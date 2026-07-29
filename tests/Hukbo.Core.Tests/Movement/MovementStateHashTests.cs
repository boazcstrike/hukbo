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
}
