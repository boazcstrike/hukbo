using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The conditional state-hash contract of the weapon-relative movement
/// design, sections 14.2 and 14.3: <c>StateHasher.Compute</c> gains one
/// trailing <c>ulong? movementContentHash</c> parameter following the
/// <c>hasRankLevels</c> precedent. When it is <see langword="null"/> -- every
/// preset V1 through V5 -- the fold is byte-for-byte the legacy layout, so a
/// footwork field a legacy preset never writes can never reach a legacy
/// hash. When it is non-null -- V6 only -- the movement content hash folds
/// immediately after the combat content hash, and the five footwork fields
/// fold at the tail of each per-agent fold, after the conditional rank fold,
/// in <c>AgentState</c> declaration order.
/// </summary>
public sealed class MovementStateHashTests
{
    /// <summary>
    /// An arbitrary, fixed combat content hash for the direct hasher calls.
    /// Deliberately different from <see cref="MovementContentHash"/> so a
    /// swap of the two content-hash folds cannot cancel out.
    /// </summary>
    private const ulong CombatContentHash = 0xFEDCBA9876543210UL;

    /// <summary>
    /// An arbitrary, fixed movement content hash for the direct hasher
    /// calls. The V6 wiring test uses the registered ruleset value instead.
    /// </summary>
    private const ulong MovementContentHash = 0x0123456789ABCDEFUL;

    // ----- The null path: byte-for-byte legacy -----

    /// <summary>
    /// The five footwork fields never reach a null-path hash in either
    /// <c>hasRankLevels</c> state: writing all five to non-default values
    /// leaves the legacy fold untouched, which is the "no new writes
    /// anywhere" half of the design 14.2 contract. The pinned pairs in
    /// <c>DeterminismTests</c> and the trajectory freeze fixtures prove the
    /// same thing end-to-end; this is the direct, single-agent form.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheNullPathIgnoresAllFiveFootworkFields(bool hasRankLevels)
    {
        var scenario = CreateScenario(MovementPresetId.PersistentContingentsV4);
        var agent = CreateAgent(scenario);
        var baseline = ComputeHash(
            scenario, agent, hasRankLevels, movementContentHash: null);

        WriteDistinctiveFootworkFields(agent);
        var mutated = ComputeHash(
            scenario, agent, hasRankLevels, movementContentHash: null);

        Assert.Equal(baseline, mutated);
    }

    /// <summary>
    /// The trailing parameter defaults to <see langword="null"/>, so every
    /// call site written before it existed is the null path by construction.
    /// </summary>
    [Fact]
    public void OmittingTheTrailingParameterIsTheNullPath()
    {
        var scenario = CreateScenario(MovementPresetId.PersistentContingentsV4);
        var agent = CreateAgent(scenario);
        WriteDistinctiveFootworkFields(agent);

        var omitted = StateHasher.Compute(
            scenario,
            tick: 1,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents: [agent],
            contentHash: CombatContentHash,
            hasRankLevels: false);
        var explicitNull = ComputeHash(
            scenario, agent, hasRankLevels: false, movementContentHash: null);

        Assert.Equal(omitted, explicitNull);
    }

    // ----- The V6 path: every new input reaches the hash -----

    /// <summary>
    /// Acceptance for row T7a: changing any one of the five footwork fields
    /// changes the V6 hash. Each case mutates exactly one field against an
    /// otherwise identical agent.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EachFootworkFieldReachesTheVSixHash(int fieldIndex)
    {
        var scenario = CreateScenario(MovementPresetId.EquipmentRelativeFootworkV6);
        var agent = CreateAgent(scenario);
        var baseline = ComputeHash(
            scenario, agent, hasRankLevels: false, MovementContentHash);

        switch (fieldIndex)
        {
            case 0:
                agent.Facing = Facing16.East;
                break;
            case 1:
                agent.MovementPaceRaw = 37;
                break;
            case 2:
                agent.TacticalPosture = TacticalPosture.Advance;
                break;
            case 3:
                agent.FootworkPhase = FootworkPhase.Approach;
                break;
            default:
                agent.FootworkTicksRemaining = 3;
                break;
        }

        var mutated = ComputeHash(
            scenario, agent, hasRankLevels: false, MovementContentHash);

        Assert.NotEqual(baseline, mutated);
    }

    /// <summary>
    /// The movement content hash itself reaches the V6 hash, which is what
    /// makes every profile scalar and offset cell hash-visible: the ruleset
    /// folds its whole tuning surface into
    /// <see cref="MovementRuleset.ContentHash"/>, so changing any profile
    /// value changes this input and therefore every V6 state hash.
    /// </summary>
    [Fact]
    public void TheMovementContentHashReachesTheVSixHash()
    {
        var scenario = CreateScenario(MovementPresetId.EquipmentRelativeFootworkV6);
        var agent = CreateAgent(scenario);

        var first = ComputeHash(
            scenario, agent, hasRankLevels: false, MovementContentHash);
        var second = ComputeHash(
            scenario, agent, hasRankLevels: false, MovementContentHash + 1);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// <see cref="Facing16.None"/> folds as its numeric 255, not as a
    /// normalised zero: the enum default numeric value is
    /// <see cref="Facing16.East"/> (0), so collapsing the two would make a
    /// spawned-but-unturned warrior and a legacy-idle one hash identically.
    /// </summary>
    [Fact]
    public void FacingNoneFoldsAsTwoHundredFiftyFive()
    {
        Assert.Equal(255, (int)Facing16.None);

        var scenario = CreateScenario(MovementPresetId.EquipmentRelativeFootworkV6);
        var agent = CreateAgent(scenario);
        Assert.Equal(Facing16.None, agent.Facing);
        var none = ComputeHash(
            scenario, agent, hasRankLevels: false, MovementContentHash);

        agent.Facing = Facing16.East;
        var east = ComputeHash(
            scenario, agent, hasRankLevels: false, MovementContentHash);

        Assert.NotEqual(none, east);
    }

    // ----- The V6 fold order, pinned -----

    /// <summary>
    /// The exact V6 fold layout, pinned as recorded literals on a tiny
    /// deterministic input so an accidental reorder -- folding the movement
    /// content hash anywhere but immediately after the combat content hash,
    /// or folding the five footwork fields in any order but declaration
    /// order at the per-agent tail -- fails here. All five field values and
    /// both content hashes are pairwise distinct, so no swap can cancel out.
    /// Two literals pin both <c>hasRankLevels</c> states, proving the five
    /// fields follow the conditional rank fold rather than displacing it.
    /// Captured from a real run of this exact input against this build.
    /// </summary>
    [Theory]
    [InlineData(false, 0xC7B6C46A3D086571UL)]
    [InlineData(true, 0x2F465B4A80E658B2UL)]
    public void TheVSixFoldOrderIsPinned(bool hasRankLevels, ulong expected)
    {
        var scenario = CreateScenario(MovementPresetId.EquipmentRelativeFootworkV6);
        var agent = CreateAgent(scenario);
        WriteDistinctiveFootworkFields(agent);

        var hash = StateHasher.Compute(
            scenario,
            tick: 11,
            BattleOutcome.Ongoing,
            eventSequence: 4,
            agents: [agent],
            contentHash: CombatContentHash,
            hasRankLevels: hasRankLevels,
            movementContentHash: MovementContentHash);

        Assert.Equal(expected, hash);
    }

    // ----- The BattleSimulation wiring -----

    /// <summary>
    /// A legacy-preset simulation passes <see langword="null"/>: its hash is
    /// the plain legacy fold of its own agent states, and folding the
    /// registered movement content hash would move it.
    /// </summary>
    [Fact]
    public void ALegacyPresetSimulationHashesTheNullPath()
    {
        var scenario = CreateScenario(MovementPresetId.PersistentContingentsV4);
        var agents = CreateRoster(scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);

        var expected = StateHasher.Compute(
            scenario,
            tick: 0,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents,
            rules.ContentHash,
            rules.HasRankLevels);
        var withMovementFold = StateHasher.Compute(
            scenario,
            tick: 0,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents,
            rules.ContentHash,
            rules.HasRankLevels,
            MovementPresetRegistry.Get(scenario.MovementPreset).ContentHash);

        Assert.Equal(expected, simulation.ComputeStateHash());
        Assert.NotEqual(withMovementFold, simulation.ComputeStateHash());
    }

    /// <summary>
    /// A V6 simulation passes its own
    /// <see cref="MovementRuleset.ContentHash"/>: its hash is the V6 fold of
    /// its own agent states, and the null path would move it.
    /// </summary>
    [Fact]
    public void AVSixSimulationFoldsItsMovementContentHash()
    {
        var scenario = CreateScenario(MovementPresetId.EquipmentRelativeFootworkV6);
        var agents = CreateRoster(scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
        var movement = MovementPresetRegistry.Get(scenario.MovementPreset);

        var expected = StateHasher.Compute(
            scenario,
            tick: 0,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents,
            rules.ContentHash,
            rules.HasRankLevels,
            movement.ContentHash);
        var nullPath = StateHasher.Compute(
            scenario,
            tick: 0,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents,
            rules.ContentHash,
            rules.HasRankLevels);

        Assert.Equal(expected, simulation.ComputeStateHash());
        Assert.NotEqual(nullPath, simulation.ComputeStateHash());
    }

    // ----- V7 gate: the pressure-interrupt parameter leaves V1 to V6 alone -----

    /// <summary>
    /// The second trailing parameter, <c>appliesPressureInterrupt</c>, defaults
    /// to <see langword="false"/> exactly as <c>hasRankLevels</c> and
    /// <c>movementContentHash</c> before it. For every preset up to and
    /// including V6 the registered gate is <see langword="false"/>, so the call
    /// written before the parameter existed and the call that passes it
    /// explicitly must fold the identical byte sequence. The agent carries all
    /// five footwork fields and all three pressure fields at distinctive
    /// values, so a fold that reached any of them under a false gate would show
    /// up here.
    /// </summary>
    [Theory]
    [InlineData(MovementPresetId.IndependentPursuitV1)]
    [InlineData(MovementPresetId.PersistentContingentsV2)]
    [InlineData(MovementPresetId.PersistentContingentsV3)]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.PersistentContingentsV5)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    public void TheDefaultedPressureParameterMatchesAnExplicitFalse(
        MovementPresetId presetId)
    {
        var movement = MovementPresetRegistry.Get(presetId);
        Assert.False(movement.AppliesPressureInterrupt);

        var scenario = CreateScenario(presetId);
        var agent = CreateAgent(scenario);
        WriteDistinctiveFootworkFields(agent);
        WriteDistinctivePressureFields(agent);

        var movementContentHash = movement.UsesEquipmentRelativeFootwork
            ? movement.ContentHash
            : (ulong?)null;

        var omitted = StateHasher.Compute(
            scenario,
            tick: 1,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents: [agent],
            contentHash: CombatContentHash,
            hasRankLevels: false,
            movementContentHash: movementContentHash);
        var explicitFalse = StateHasher.Compute(
            scenario,
            tick: 1,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents: [agent],
            contentHash: CombatContentHash,
            hasRankLevels: false,
            movementContentHash: movementContentHash,
            appliesPressureInterrupt: false);

        Assert.Equal(omitted, explicitFalse);
    }

    /// <summary>
    /// The same claim against what each preset actually produces today: a
    /// simulation on any preset up to and including V6 hashes exactly what the
    /// pre-V7 call shape — the one that stops at
    /// <c>movementContentHash</c> — produces for its own agent states. This is
    /// the regression that would fail if the new parameter were folded
    /// unconditionally, or if <c>BattleSimulation</c> passed it as anything but
    /// the registered gate.
    /// </summary>
    [Theory]
    [InlineData(MovementPresetId.IndependentPursuitV1)]
    [InlineData(MovementPresetId.PersistentContingentsV2)]
    [InlineData(MovementPresetId.PersistentContingentsV3)]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.PersistentContingentsV5)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    public void EveryPresetUpToVSixStillHashesThePreVSevenCallShape(
        MovementPresetId presetId)
    {
        var scenario = CreateScenario(presetId);
        var agents = CreateRoster(scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
        var movement = MovementPresetRegistry.Get(presetId);

        var preVSevenShape = StateHasher.Compute(
            scenario,
            tick: 0,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents,
            rules.ContentHash,
            rules.HasRankLevels,
            movement.UsesEquipmentRelativeFootwork
                ? movement.ContentHash
                : null);

        Assert.Equal(preVSevenShape, simulation.ComputeStateHash());
    }

    // ----- V7 path: the three pressure fields reach the hash -----

    /// <summary>
    /// Design section 6.2 item 3: under a true gate the three pressure fields
    /// fold at the tail of the per-agent fold. Each case mutates exactly one of
    /// them against an otherwise identical agent, so a field left out of the
    /// fold fails its own case rather than hiding behind another.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachPressureFieldReachesTheVSevenHash(int fieldIndex)
    {
        var scenario = CreateScenario(MovementPresetId.EquipmentRelativeFootworkV7);
        var agent = CreateAgent(scenario);
        var baseline = ComputeHash(
            scenario,
            agent,
            hasRankLevels: false,
            MovementContentHash,
            appliesPressureInterrupt: true);

        MutateOnePressureField(agent, fieldIndex);

        var mutated = ComputeHash(
            scenario,
            agent,
            hasRankLevels: false,
            MovementContentHash,
            appliesPressureInterrupt: true);

        Assert.NotEqual(baseline, mutated);
    }

    /// <summary>
    /// The mirror image, and the reason the gate is a parameter of its own
    /// rather than a reuse of <c>movementContentHash</c>: under a false gate
    /// none of the three fields reaches the hash, whether or not the movement
    /// content hash is being folded. The <see langword="true"/> cases are the
    /// V6 shape — a non-null movement content hash with a false gate — and are
    /// what keeps the frozen V6 trajectory digest where it is.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void NoPressureFieldReachesTheHashUnderAFalseGate(
        int fieldIndex, bool foldsMovementContentHash)
    {
        var scenario = CreateScenario(foldsMovementContentHash
            ? MovementPresetId.EquipmentRelativeFootworkV6
            : MovementPresetId.PersistentContingentsV4);
        var agent = CreateAgent(scenario);
        var movementContentHash = foldsMovementContentHash
            ? MovementContentHash
            : (ulong?)null;
        var baseline = ComputeHash(
            scenario,
            agent,
            hasRankLevels: false,
            movementContentHash,
            appliesPressureInterrupt: false);

        MutateOnePressureField(agent, fieldIndex);

        var mutated = ComputeHash(
            scenario,
            agent,
            hasRankLevels: false,
            movementContentHash,
            appliesPressureInterrupt: false);

        Assert.Equal(baseline, mutated);
    }

    /// <summary>
    /// The two integer pressure fields occupy distinct positions in the fold,
    /// so moving a value from one to the other moves the hash. A fold that
    /// wrote them in either order, or summed them, would collapse these two
    /// agents onto one hash.
    /// </summary>
    [Fact]
    public void TheTwoIntegerPressureFieldsFoldInDistinctPositions()
    {
        var scenario = CreateScenario(MovementPresetId.EquipmentRelativeFootworkV7);

        var damageOnly = CreateAgent(scenario);
        damageOnly.DamageTakenLastTick = 9;
        damageOnly.PriorSupportAllies = 0;

        var alliesOnly = CreateAgent(scenario);
        alliesOnly.DamageTakenLastTick = 0;
        alliesOnly.PriorSupportAllies = 9;

        var first = ComputeHash(
            scenario,
            damageOnly,
            hasRankLevels: false,
            MovementContentHash,
            appliesPressureInterrupt: true);
        var second = ComputeHash(
            scenario,
            alliesOnly,
            hasRankLevels: false,
            MovementContentHash,
            appliesPressureInterrupt: true);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// A V7 simulation hands its own registered gate to the hasher, exactly as
    /// a V6 simulation hands its own movement content hash. The false-gate call
    /// would move the result, which is what makes the gate observable end to
    /// end rather than only at the hasher's own boundary.
    /// </summary>
    [Fact]
    public void AVSevenSimulationFoldsThePressureInterruptGate()
    {
        var scenario = CreateScenario(MovementPresetId.EquipmentRelativeFootworkV7);
        var agents = CreateRoster(scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
        var movement = MovementPresetRegistry.Get(scenario.MovementPreset);
        Assert.True(movement.AppliesPressureInterrupt);

        var expected = StateHasher.Compute(
            scenario,
            tick: 0,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents,
            rules.ContentHash,
            rules.HasRankLevels,
            movement.ContentHash,
            appliesPressureInterrupt: true);
        var falseGate = StateHasher.Compute(
            scenario,
            tick: 0,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents,
            rules.ContentHash,
            rules.HasRankLevels,
            movement.ContentHash);

        Assert.Equal(expected, simulation.ComputeStateHash());
        Assert.NotEqual(falseGate, simulation.ComputeStateHash());
    }

    // ----- V7 ruleset values reach the content hash -----

    /// <summary>
    /// The three pressure-interrupt weights reach
    /// <see cref="MovementRuleset.ContentHash"/> under a true gate.
    /// <para>
    /// Each case redistributes the weight between exactly two of the three
    /// positions and leaves the third alone. It is deliberately a
    /// redistribution rather than a single-weight change, because a
    /// single-weight change cannot be constructed: design section 6.3 and
    /// <c>MovementRuleset.ValidatePressureInterruptCoupling</c> require the
    /// three weights to total exactly
    /// <see cref="MovementRuleset.TotalPressureInterruptWeightBasisPoints"/>,
    /// so two legal rulesets always differ in at least two of them. The
    /// companion test
    /// <see cref="AVSevenPresetWhoseWeightsDoNotTotalTenThousandIsRejected"/>
    /// pins that constraint so this substitution stays visible.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(3_000, 5_000, 2_000)]
    [InlineData(5_000, 2_000, 3_000)]
    [InlineData(2_000, 3_000, 5_000)]
    public void RedistributingThePressureWeightsMovesTheVSevenContentHash(
        int supportWeight, int incomingDamageWeight, int allyCollapseWeight)
    {
        var rows = CreateProfileRows(thresholdBasisPoints: 4_000);
        var baseline = CreateRuleset(
            rows,
            appliesPressureInterrupt: true,
            supportPressureWeightBasisPoints: 5_000,
            incomingDamageWeightBasisPoints: 3_000,
            allyCollapseWeightBasisPoints: 2_000);
        var variant = CreateRuleset(
            rows,
            appliesPressureInterrupt: true,
            supportPressureWeightBasisPoints: supportWeight,
            incomingDamageWeightBasisPoints: incomingDamageWeight,
            allyCollapseWeightBasisPoints: allyCollapseWeight);

        Assert.NotEqual(baseline.ContentHash, variant.ContentHash);
    }

    /// <summary>
    /// Design section 6.2 item 2: every profile row's
    /// <see cref="LoadoutMovementProfile.PressureInterruptThresholdBasisPoints"/>
    /// folds inside the same gate, one layer down. Two rulesets differing only
    /// in one row's threshold hash differently, and each of the six rows gets
    /// its own case, so a fold that read only the first row, or that read the
    /// rows in the wrong order, fails here.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EachRowsPressureThresholdMovesTheVSevenContentHash(int rowIndex)
    {
        var baseline = CreateRuleset(
            CreateProfileRows(thresholdBasisPoints: 4_000),
            appliesPressureInterrupt: true,
            supportPressureWeightBasisPoints: 5_000,
            incomingDamageWeightBasisPoints: 3_000,
            allyCollapseWeightBasisPoints: 2_000);
        var variant = CreateRuleset(
            CreateProfileRows(
                thresholdBasisPoints: 4_000,
                variantRowIndex: rowIndex,
                variantThresholdBasisPoints: 4_001),
            appliesPressureInterrupt: true,
            supportPressureWeightBasisPoints: 5_000,
            incomingDamageWeightBasisPoints: 3_000,
            allyCollapseWeightBasisPoints: 2_000);

        Assert.NotEqual(baseline.ContentHash, variant.ContentHash);
    }

    // ----- the gate, not the field, is what moves the content hash -----

    /// <summary>
    /// The direct proof that the version gate is what moves the hash. Two
    /// rulesets identical in every other field — same identity, same cohesion
    /// tunables, same radii, same six profile rows and every scalar on them —
    /// hash differently purely because one applies the interrupt and one does
    /// not.
    /// <para>
    /// The plan's own wording for this row asked instead for the two weight and
    /// threshold variants to be rebuilt under a false gate and shown to hash
    /// identically. That cannot be constructed: the false-gate branch of
    /// <c>ValidatePressureInterruptCoupling</c> rejects a non-zero weight and a
    /// non-zero row threshold outright, so the false-gate variants carrying
    /// those differing values do not exist to be compared. The two rejection
    /// tests below assert that, and this test carries the intent the plan row
    /// was reaching for.
    /// </para>
    /// </summary>
    [Fact]
    public void TheGateNotTheValueMovesTheContentHash()
    {
        var falseGate = CreateRuleset(CreateProfileRows());
        var trueGate = CreateRuleset(
            CreateProfileRows(thresholdBasisPoints: 4_000),
            appliesPressureInterrupt: true,
            supportPressureWeightBasisPoints: 5_000,
            incomingDamageWeightBasisPoints: 3_000,
            allyCollapseWeightBasisPoints: 2_000);

        Assert.False(falseGate.AppliesPressureInterrupt);
        Assert.True(trueGate.AppliesPressureInterrupt);
        Assert.NotEqual(falseGate.ContentHash, trueGate.ContentHash);
    }

    /// <summary>
    /// The other half of the same proof, and the one that actually protects the
    /// frozen presets: V6 is a false-gate preset, and its
    /// <see cref="MovementRuleset.ContentHash"/> is still the value pinned as
    /// <c>EquipmentRelativeFootworkV6ContentHash</c> in
    /// <c>MovementPresetRegistryTests</c>. The literal below is quoted from
    /// that pin, not newly recorded; if it ever has to be edited here, the gate
    /// has leaked and the frozen V6 trajectory digest has moved with it.
    /// </summary>
    [Fact]
    public void TheFalseGateLeavesTheFrozenVSixContentHashWhereItIs()
    {
        var v6 = MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV6);
        var v7 = MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV7);

        Assert.False(v6.AppliesPressureInterrupt);
        Assert.Equal(0x0FFE5D202B324D25UL, v6.ContentHash);

        Assert.True(v7.AppliesPressureInterrupt);
        Assert.NotEqual(v6.ContentHash, v7.ContentHash);
    }

    /// <summary>
    /// The ruleset-side counterpart of
    /// <see cref="OmittingTheTrailingParameterIsTheNullPath"/>: the four
    /// pressure values are trailing optional constructor parameters, so a
    /// construction site written before they existed folds exactly what a site
    /// that spells out a false gate and three zero weights folds.
    /// </summary>
    [Fact]
    public void OmittingTheTrailingPressureParametersMatchesAnExplicitFalse()
    {
        var rows = CreateProfileRows();
        var omitted = CreateRulesetWithoutPressureParameters(rows);
        var spelledOut = CreateRuleset(
            rows,
            appliesPressureInterrupt: false,
            supportPressureWeightBasisPoints: 0,
            incomingDamageWeightBasisPoints: 0,
            allyCollapseWeightBasisPoints: 0);

        Assert.Equal(omitted.ContentHash, spelledOut.ContentHash);
    }

    // ----- construction-time coupling: the unconstructable variants -----

    /// <summary>
    /// Design section 6.3, first clause. A preset that does not apply the
    /// interrupt must register three zero weights, so a false-gate ruleset
    /// carrying a differing weight cannot be constructed at all. Note the
    /// exception type: this clause throws
    /// <see cref="ArgumentOutOfRangeException"/>, while the row-threshold
    /// clause below throws a plain <see cref="ArgumentException"/>, and xunit's
    /// <c>Assert.Throws</c> matches the type exactly.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AFalseGatePresetWithANonZeroWeightIsRejected(int weightIndex)
    {
        var rows = CreateProfileRows();

        var rejected = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateRuleset(
                rows,
                appliesPressureInterrupt: false,
                supportPressureWeightBasisPoints: weightIndex == 0 ? 1 : 0,
                incomingDamageWeightBasisPoints: weightIndex == 1 ? 1 : 0,
                allyCollapseWeightBasisPoints: weightIndex == 2 ? 1 : 0));

        Assert.Equal(
            weightIndex switch
            {
                0 => "supportPressureWeightBasisPoints",
                1 => "incomingDamageWeightBasisPoints",
                _ => "allyCollapseWeightBasisPoints",
            },
            rejected.ParamName);
    }

    /// <summary>
    /// Design section 6.3, the per-row half of the first clause. A false-gate
    /// ruleset carrying a differing row threshold cannot be constructed either,
    /// whichever of the six rows carries it. This clause throws a plain
    /// <see cref="ArgumentException"/> naming the profile collection, not the
    /// <see cref="ArgumentOutOfRangeException"/> the weight clause throws.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void AFalseGatePresetWithANonZeroRowThresholdIsRejected(int rowIndex)
    {
        var rows = CreateProfileRows(
            variantRowIndex: rowIndex, variantThresholdBasisPoints: 4_000);

        var rejected = Assert.Throws<ArgumentException>(
            () => CreateRuleset(rows));

        Assert.Equal("loadoutMovementProfiles", rejected.ParamName);
    }

    /// <summary>
    /// Design section 6.3, second clause, and the constraint that makes a
    /// single-weight V7 variant unconstructable: the three weights must total
    /// exactly
    /// <see cref="MovementRuleset.TotalPressureInterruptWeightBasisPoints"/>,
    /// so moving one weight on its own always fails construction and the
    /// weight test above has to redistribute instead.
    /// </summary>
    [Fact]
    public void AVSevenPresetWhoseWeightsDoNotTotalTenThousandIsRejected()
    {
        var rows = CreateProfileRows(thresholdBasisPoints: 4_000);

        var rejected = Assert.Throws<ArgumentException>(
            () => CreateRuleset(
                rows,
                appliesPressureInterrupt: true,
                supportPressureWeightBasisPoints: 5_001,
                incomingDamageWeightBasisPoints: 3_000,
                allyCollapseWeightBasisPoints: 2_000));

        Assert.Equal("supportPressureWeightBasisPoints", rejected.ParamName);
    }

    // ----- Helpers -----

    private static Scenario CreateScenario(MovementPresetId movementPreset) =>
        new Scenario(
            Seed: 3,
            MapWidth: 200,
            MapHeight: 200,
            AgentsPerFaction: 2,
            TickRate: 20,
            TickLimit: 1_000) with
        {
            MovementPreset = movementPreset,
        };

    /// <summary>
    /// Writes all five footwork fields to non-default, pairwise-distinct
    /// values, so that a null-path fold that reads any of them and a V6 fold
    /// that swaps any two of them both change the result.
    /// </summary>
    private static void WriteDistinctiveFootworkFields(AgentState agent)
    {
        agent.Facing = Facing16.SouthEast; // numeric 2
        agent.MovementPaceRaw = 37;
        agent.TacticalPosture = TacticalPosture.Yield; // numeric 3
        agent.FootworkPhase = FootworkPhase.Recover; // numeric 4
        agent.FootworkTicksRemaining = 5;
    }

    private static AgentState[] CreateRoster(Scenario scenario) =>
    [
        CreateAgent(scenario, entityId: 1, factionId: 0, xRaw: 10_240, yRaw: 25_600),
        CreateAgent(scenario, entityId: 2, factionId: 1, xRaw: 40_960, yRaw: 25_600),
    ];

    private static AgentState CreateAgent(
        Scenario scenario,
        ulong entityId = 7,
        int factionId = 1,
        int xRaw = 1_024,
        int yRaw = 2_048) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));

    private static ulong ComputeHash(
        Scenario scenario,
        AgentState agent,
        bool hasRankLevels,
        ulong? movementContentHash) =>
        StateHasher.Compute(
            scenario,
            tick: 1,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents: [agent],
            contentHash: CombatContentHash,
            hasRankLevels: hasRankLevels,
            movementContentHash: movementContentHash);

    private static ulong ComputeHash(
        Scenario scenario,
        AgentState agent,
        bool hasRankLevels,
        ulong? movementContentHash,
        bool appliesPressureInterrupt) =>
        StateHasher.Compute(
            scenario,
            tick: 1,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents: [agent],
            contentHash: CombatContentHash,
            hasRankLevels: hasRankLevels,
            movementContentHash: movementContentHash,
            appliesPressureInterrupt: appliesPressureInterrupt);

    /// <summary>
    /// Writes all three pressure-interrupt fields to non-default values, so a
    /// false-gate fold that read any of them would change its result.
    /// </summary>
    private static void WriteDistinctivePressureFields(AgentState agent)
    {
        agent.DamageTakenLastTick = 11;
        agent.PriorSupportAllies = 4;
        agent.BrokeOffUnderPressure = true;
    }

    /// <summary>
    /// Moves exactly one pressure-interrupt field off its default, indexed in
    /// <see cref="AgentState"/> declaration order.
    /// </summary>
    private static void MutateOnePressureField(AgentState agent, int fieldIndex)
    {
        switch (fieldIndex)
        {
            case 0:
                agent.DamageTakenLastTick = 11;
                break;
            case 1:
                agent.PriorSupportAllies = 4;
                break;
            default:
                agent.BrokeOffUnderPressure = true;
                break;
        }
    }

    /// <summary>
    /// The six canonical loadout keys, in the <c>KP, WA, KA, IT, KS, IS</c>
    /// order <see cref="MovementRuleset"/> validates a profile collection
    /// against.
    /// </summary>
    private static readonly CombatLoadout[] CanonicalLoadouts =
    [
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
    ];

    /// <summary>
    /// Six signed opponent-distance offset cells, pairwise distinct and inside
    /// every design 4.3 bound for the preferred distance below.
    /// </summary>
    private static readonly ImmutableArray<int> OpponentOffsets =
        [0, 100, -100, 200, -200, 50];

    /// <summary>
    /// Builds the six canonical profile rows, all carrying
    /// <paramref name="thresholdBasisPoints"/> except the row at
    /// <paramref name="variantRowIndex"/>, which carries
    /// <paramref name="variantThresholdBasisPoints"/>. Every other scalar is
    /// identical across rows and across calls, so two rulesets built from two
    /// of these collections differ in exactly the threshold cells named here.
    /// The values are arbitrary test fixtures chosen to satisfy the design 4.3
    /// bounds, not any shipped preset's tuning: nothing here reads or asserts a
    /// registered value, so the tuning task is free to move every one of them.
    /// </summary>
    private static ImmutableArray<LoadoutMovementProfile> CreateProfileRows(
        int thresholdBasisPoints = 0,
        int variantRowIndex = -1,
        int variantThresholdBasisPoints = 0)
    {
        var builder = ImmutableArray.CreateBuilder<LoadoutMovementProfile>(
            MovementRuleset.CanonicalLoadoutCount);
        for (var row = 0; row < MovementRuleset.CanonicalLoadoutCount; row++)
        {
            builder.Add(new LoadoutMovementProfile(
                CanonicalLoadouts[row],
                forwardPaceBasisPoints: 9_000,
                lateralPaceBasisPoints: 7_000,
                backwardPaceBasisPoints: 5_000,
                committedPaceBasisPoints: 4_000,
                preferredDistanceBasisPoints: 3_000,
                opponentDistanceOffsetBasisPoints: OpponentOffsets,
                maximumFacingStepsPerTick: 4,
                committedFacingStepsPerTick: 1,
                accelerationBasisPointsPerTick: 2_000,
                decelerationBasisPointsPerTick: 3_000,
                commitmentTicks: 6,
                recoveryTicks: 4,
                allyClearanceBodyDiametersBasisPoints: 12_000,
                disengageEnemyToAllyBasisPoints: 20_000,
                reengageEnemyToAllyBasisPoints: 15_000,
                pursuitSupportBodyDiametersBasisPoints: 30_000,
                pressureInterruptThresholdBasisPoints: row == variantRowIndex
                    ? variantThresholdBasisPoints
                    : thresholdBasisPoints));
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Builds a ruleset whose every non-pressure field is fixed, so two
    /// rulesets from this helper differ only in the pressure values the caller
    /// varies. The cohesion tunables are arbitrary test fixtures, not a shipped
    /// preset's tuning.
    /// </summary>
    private static MovementRuleset CreateRuleset(
        ImmutableArray<LoadoutMovementProfile> profiles,
        bool appliesPressureInterrupt = false,
        int supportPressureWeightBasisPoints = 0,
        int incomingDamageWeightBasisPoints = 0,
        int allyCollapseWeightBasisPoints = 0) =>
        new(
            id: MovementPresetId.EquipmentRelativeFootworkV7,
            version: 1,
            cohesionRadiusMultiplier: 24,
            closeRadiusMultiplier: 16,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            cohesionCycleTicks: 240,
            cohesionDutyTicks: 180,
            arrivalTaperMultiplier: 4,
            offsetUnit: 1_024,
            narrowsCohesionScanToCohesionCapableContingents: true,
            selectsLeaderByRank: true,
            usesEquipmentRelativeFootwork: true,
            immediateRadiusBodyDiametersBasisPoints: 15_000,
            supportRadiusBodyDiametersBasisPoints: 30_000,
            loadoutMovementProfiles: profiles,
            appliesPressureInterrupt: appliesPressureInterrupt,
            supportPressureWeightBasisPoints: supportPressureWeightBasisPoints,
            incomingDamageWeightBasisPoints: incomingDamageWeightBasisPoints,
            allyCollapseWeightBasisPoints: allyCollapseWeightBasisPoints);

    /// <summary>
    /// The same ruleset written the way a construction site that predates the
    /// four trailing pressure parameters writes it: it stops at the profile
    /// collection and names none of them.
    /// </summary>
    private static MovementRuleset CreateRulesetWithoutPressureParameters(
        ImmutableArray<LoadoutMovementProfile> profiles) =>
        new(
            id: MovementPresetId.EquipmentRelativeFootworkV7,
            version: 1,
            cohesionRadiusMultiplier: 24,
            closeRadiusMultiplier: 16,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            cohesionCycleTicks: 240,
            cohesionDutyTicks: 180,
            arrivalTaperMultiplier: 4,
            offsetUnit: 1_024,
            narrowsCohesionScanToCohesionCapableContingents: true,
            selectsLeaderByRank: true,
            usesEquipmentRelativeFootwork: true,
            immediateRadiusBodyDiametersBasisPoints: 15_000,
            supportRadiusBodyDiametersBasisPoints: 30_000,
            loadoutMovementProfiles: profiles);
}
