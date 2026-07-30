using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The friendly-clearance conflict pass of the weapon-relative movement
/// design, section 10.6: the phase-safety-then-EntityId total order, the
/// equality-accepts boundary, the freed-space consequence of a rejection,
/// the exact-set match against <see cref="NaiveConflictPassOracle"/> across
/// seeded scenarios, and the pass's wiring inside the tick pipeline —
/// rejection to a no-move with zero retained pace and no phase change,
/// faction locality, and the legacy control.
/// </summary>
public sealed class MovementConflictPassTests
{
    // ----- The pure pass -----

    [Fact]
    public void EndpointsExactlyAtTheClearanceRadiusBothAccept()
    {
        // Both clearance radii squared are 1536^2; the endpoints sit exactly
        // 1536 apart, and equality accepts.
        var clearance = Square(1_536);
        var proposals = new[]
        {
            new FriendlyClearanceProposal(1, FootworkPhase.Approach, 0, 0, clearance),
            new FriendlyClearanceProposal(2, FootworkPhase.Approach, 1_536, 0, clearance),
        };

        Assert.Equal([true, true], RunPass(proposals));
    }

    [Fact]
    public void AnEndpointStrictlyInsideTheClearanceIsRejected()
    {
        var clearance = Square(1_536);
        var proposals = new[]
        {
            new FriendlyClearanceProposal(1, FootworkPhase.Approach, 0, 0, clearance),
            new FriendlyClearanceProposal(2, FootworkPhase.Approach, 1_535, 0, clearance),
        };

        Assert.Equal([true, false], RunPass(proposals));
    }

    [Fact]
    public void TheLargerOfTheTwoClearanceRadiiGoverns()
    {
        // The second proposal wants only 500 clear, but the first wants
        // 1536, and the larger radius governs the pair.
        var proposals = new[]
        {
            new FriendlyClearanceProposal(1, FootworkPhase.Approach, 0, 0, Square(1_536)),
            new FriendlyClearanceProposal(2, FootworkPhase.Approach, 1_000, 0, Square(500)),
        };

        Assert.Equal([true, false], RunPass(proposals));
    }

    [Fact]
    public void PhaseSafetyOutranksEntityId()
    {
        // The disengaging warrior carries the higher EntityId but the safer
        // phase, so it is placed first and the approaching warrior with the
        // lower id loses the contested ground.
        var clearance = Square(1_536);
        var proposals = new[]
        {
            new FriendlyClearanceProposal(1, FootworkPhase.Approach, 0, 0, clearance),
            new FriendlyClearanceProposal(9, FootworkPhase.Disengage, 1_000, 0, clearance),
        };

        Assert.Equal([false, true], RunPass(proposals));
    }

    [Fact]
    public void WithinOnePhaseTheLowerEntityIdWins()
    {
        var clearance = Square(1_536);
        var proposals = new[]
        {
            new FriendlyClearanceProposal(3, FootworkPhase.Engage, 0, 0, clearance),
            new FriendlyClearanceProposal(4, FootworkPhase.Engage, 1_000, 0, clearance),
        };

        Assert.Equal([true, false], RunPass(proposals));
    }

    [Fact]
    public void ARejectedProposalDoesNotBlockALaterOne()
    {
        // The middle proposal is rejected against the first; the third
        // conflicts only with the rejected middle one, so it is accepted —
        // rejection frees the ground it would have taken.
        var clearance = Square(1_536);
        var proposals = new[]
        {
            new FriendlyClearanceProposal(1, FootworkPhase.Approach, 0, 0, clearance),
            new FriendlyClearanceProposal(2, FootworkPhase.Approach, 1_500, 0, clearance),
            new FriendlyClearanceProposal(3, FootworkPhase.Approach, 3_000, 0, clearance),
        };

        Assert.Equal([true, false, true], RunPass(proposals));
    }

    [Fact]
    public void ProposalsMustArriveInAscendingEntityIdOrder()
    {
        var clearance = Square(1_536);
        var proposals = new[]
        {
            new FriendlyClearanceProposal(2, FootworkPhase.Approach, 0, 0, clearance),
            new FriendlyClearanceProposal(1, FootworkPhase.Approach, 9_000, 0, clearance),
        };
        var accepted = new bool[2];

        Assert.Throws<ArgumentException>(
            () => MovementRouteRules.AcceptFriendlyClearanceConflicts(
                proposals, accepted));
    }

    /// <summary>
    /// Contract F of task T7b: an independent naive pairwise oracle must
    /// match the production accepted set exactly over seeded scenarios.
    /// Each seed generates a crowd of proposals with random phases,
    /// endpoints, and clearance radii dense enough that conflicts are
    /// guaranteed, and the two implementations must agree entity for
    /// entity.
    /// </summary>
    [Theory]
    [InlineData(1UL, 12)]
    [InlineData(2UL, 40)]
    [InlineData(3UL, 80)]
    [InlineData(5UL, 25)]
    [InlineData(8UL, 60)]
    public void TheNaiveOracleMatchesTheAcceptedSetExactly(ulong seed, int count)
    {
        var random = new SplitMix64(seed);
        var proposals = new FriendlyClearanceProposal[count];
        for (var index = 0; index < count; index++)
        {
            // Endpoints crowd into a 6000-unit square while clearance radii
            // run 800 through 2400, so a meaningful fraction of every crowd
            // conflicts.
            proposals[index] = new FriendlyClearanceProposal(
                (ulong)index + 1,
                (FootworkPhase)random.NextInt(9),
                random.NextInt(6_000),
                random.NextInt(6_000),
                Square(800 + random.NextInt(1_601)));
        }

        var accepted = RunPass(proposals);
        var acceptedIds = proposals
            .Where((_, index) => accepted[index])
            .Select(proposal => proposal.EntityId)
            .ToHashSet();

        var expected = NaiveConflictPassOracle.AcceptedEntityIds(proposals);
        Assert.True(acceptedIds.Count > 0, "The crowd accepted nobody.");
        Assert.True(
            acceptedIds.Count < count,
            "The crowd was not dense enough to produce any conflict, so " +
            "this seed proves nothing.");
        Assert.Equal(expected, acceptedIds);
    }

    // ----- Pipeline wiring -----

    /// <summary>
    /// Two same-faction warriors converge on one enemy from positions whose
    /// tick-start lanes are clear but whose endpoints fall inside the shared
    /// clearance radius. Every figure below is hand-computed from the design
    /// formulas: both propose 501-raw-unit steps whose endpoints sit 1358
    /// raw units apart against a 1536 clearance, so the pass accepts the
    /// lower EntityId and denies the other, which finishes the tick exactly
    /// where it started with zero retained pace and an unchanged phase.
    /// </summary>
    [Fact]
    public void ADeniedProposalBecomesANoMoveWithZeroPaceAndNoPhaseChange()
    {
        var scenario = CreateScenario();
        var lowerAgent = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var higherAgent = CreateAgent(2, factionId: 0, 51_200, 52_900, scenario);
        var enemy = CreateAgent(3, factionId: 1, 53_535, 52_050, scenario);
        lowerAgent.MovementPaceRaw = 512;
        higherAgent.MovementPaceRaw = 512;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, lowerAgent, higherAgent, enemy);

        simulation.AdvanceOneTick();

        // The lower id moved its full hand-computed step.
        Assert.Equal(51_670, lowerAgent.XRaw);
        Assert.Equal(51_371, lowerAgent.YRaw);

        // The higher id was denied: no move, zero retained pace, and its
        // Approach phase untouched — no reroute, no Refuse.
        Assert.Equal(51_200, higherAgent.XRaw);
        Assert.Equal(52_900, higherAgent.YRaw);
        Assert.Equal(0, higherAgent.MovementPaceRaw);
        Assert.Equal(FootworkPhase.Approach, higherAgent.FootworkPhase);
        Assert.Equal(1L, simulation.MovementConflictDenialsForTesting);
    }

    /// <summary>
    /// The pass is faction-local: two enemies closing head-on end the tick
    /// with endpoints far inside either clearance radius, and neither is
    /// denied — cross-faction bodies remain the collision resolver's job.
    /// </summary>
    [Fact]
    public void OpposingFactionsNeverConflictInThePass()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 53_248, 51_200, scenario);
        west.MovementPaceRaw = 512;
        east.MovementPaceRaw = 512;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        for (var tick = 0; tick < 10; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.Equal(0L, simulation.MovementConflictDenialsForTesting);
    }

    [Fact]
    public void ALegacyPresetNeverRunsTheConflictPass()
    {
        var scenario = CreateScenario() with
        {
            MovementPreset = MovementPresetId.PersistentContingentsV4,
        };
        var first = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var second = CreateAgent(2, factionId: 0, 51_200, 52_900, scenario);
        var enemy = CreateAgent(3, factionId: 1, 53_535, 52_050, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, first, second, enemy);

        for (var tick = 0; tick < 10; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.Equal(0L, simulation.MovementConflictDenialsForTesting);
    }

    // ----- Helpers -----

    private static bool[] RunPass(FriendlyClearanceProposal[] proposals)
    {
        var accepted = new bool[proposals.Length];
        MovementRouteRules.AcceptFriendlyClearanceConflicts(proposals, accepted);
        return accepted;
    }

    private static Int128 Square(long value) => (Int128)value * value;

    private static Scenario CreateScenario() =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = 1_024,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = 512,
            MovementSpeedRaw = 512,
            AttackCooldownTicks = 200,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario) =>
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
                WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None));
}
