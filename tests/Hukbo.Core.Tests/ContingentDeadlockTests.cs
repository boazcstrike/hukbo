using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// T12: the three deliberately engineered deadlock scenarios design section
/// 10.2 and 10.3 require. The twenty-seed sweep in
/// <see cref="PersistentContingentTests"/> is a screen, not a proof: it
/// samples twenty trajectories and shows that none of them stalled, which
/// does not show that the two failure geometries of section 10.2 or the
/// crossing-traffic residual of section 3.5 are survivable, because a random
/// seed may simply never produce them. Each scenario below is built to the
/// <b>worst</b> case those sections specify, not to a convenient case that
/// happens to pass -- same heading rather than opposing headings, squares
/// overlapping from tick 0, a map sized so the map-edge gate provably cannot
/// fire, and so on. Every geometric claim this file's comments make is
/// verified by hand arithmetic before being encoded, not guessed.
/// </summary>
public sealed class ContingentDeadlockTests
{
    // ------------------------------------------------------------------
    // Shape 2, the converging-squares scenario (design sections 10.2 and
    // 10.3; "TheCrossContingentGateFiresInTheConvergingSameFactionScenario"
    // is this scenario's companion fact, proving the guard actually fired).
    // ------------------------------------------------------------------

    /// <summary>
    /// Failure shape 2: two same-faction contingents on the <b>same</b>
    /// heading toward one distant shared enemy, converging with no enemy
    /// anywhere near either of them, each one's followers piling into the
    /// other's, with no casualty and no attrition to break either
    /// contingent out of it. This is Deadlock A wearing different clothes,
    /// and design section 10.2 records that no single escape reads
    /// <c>yes</c> for it: the leader exemption does not, because a leader
    /// can still be physically blocked by another contingent's mass;
    /// <see cref="ContingentState.Close"/> and <see cref="ContingentState.Break"/>
    /// do not, because neither an enemy nor a casualty is present to trigger
    /// them; the straggler gate only thins the exposure, because a member
    /// caught in the pile-up is exactly the member that has fallen behind;
    /// and the cohesion duty cycle bounds only how long any cohesive regime
    /// can last, never whether the collision resolver actually grants the
    /// movement toward it. The cross-contingent overlap gate (design section
    /// 3.5) is what has to save it, and this is the test that says whether
    /// it does.
    /// </summary>
    [Fact]
    public void TwoSameFactionContingentsWithOverlappingTrailingSquaresReachATerminalOutcome()
    {
        var scenario = ConvergingSquaresScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario, BuildConvergingSquaresRoster(scenario));

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();
        }

        Assert.True(
            simulation.Tick < scenario.TickLimit,
            "The battle failed to reach a terminal outcome before the " +
            $"{scenario.TickLimit}-tick limit; last outcome was " +
            $"{simulation.Outcome}.");
        Assert.True(
            simulation.Outcome is BattleOutcome.Faction0Victory or BattleOutcome.Faction1Victory,
            $"Expected a decisive outcome, not a forced draw; got {simulation.Outcome}.");
    }

    /// <summary>
    /// A liveness test that passes because its guard was never needed has
    /// tested nothing. This companion fact, over the identical engineered
    /// roster, asserts that on at least one tick there exists a contingent
    /// whose living non-leader spread strictly exceeds
    /// <c>cohesionRadiusRaw^2</c> -- the entry bar for
    /// <see cref="ContingentState.Hold"/> -- whose duty-cycle window is
    /// nevertheless open, and whose recorded <see cref="ContingentState"/>
    /// is <see cref="ContingentState.Advance"/> all the same. Every other
    /// route to <c>Advance</c> is excluded by construction: rules 1-3 of the
    /// state machine would have written <c>Break</c> or <c>Close</c> rather
    /// than <c>Advance</c> (no casualty, no enemy in range for the whole
    /// convergence); rule 4 is excluded because the window is checked open
    /// directly; and rule 5's own hysteresis exit bar is lower than its
    /// entry bar, so a contingent spread this far would be in <c>Hold</c>
    /// under either bar if the geometric gates passed. Only gate 6 remains.
    /// The duty-cycle window is recomputed here from its own formula, not by
    /// calling <see cref="MovementRules.IsCohesionWindowOpen"/>, so this
    /// fact does not depend on the production helper it is checking.
    /// </summary>
    [Fact]
    public void TheCrossContingentGateFiresInTheConvergingSameFactionScenario()
    {
        var scenario = ConvergingSquaresScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario, BuildConvergingSquaresRoster(scenario));
        var rules = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV2);
        var cohesionRadiusRaw = checked(
            (long)rules.CohesionRadiusMultiplier * scenario.BodyRadiusRaw);
        var cohesionRadiusSquared = checked(cohesionRadiusRaw * cohesionRadiusRaw);

        (int Slot, ulong LeaderId, ulong[] MemberIds)[] contingents =
        [
            (0, 2UL, [5UL, 6UL, 7UL]),
            (1, 12UL, [15UL, 16UL, 17UL]),
        ];

        var preTick = Snapshot(simulation);
        var fired = false;
        var diagnostics = "No tick satisfied the three-part condition.";

        while (!fired &&
            simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < Math.Min(scenario.TickLimit, 500))
        {
            simulation.AdvanceOneTick();
            var postTick = Snapshot(simulation);
            var tick = checked((int)simulation.Tick);

            foreach (var (slot, leaderId, memberIds) in contingents)
            {
                if (!preTick.TryGetValue(leaderId, out var leaderPre) || !leaderPre.IsAlive)
                {
                    continue;
                }

                var spreadSquared = 0L;
                foreach (var memberId in memberIds)
                {
                    if (!preTick.TryGetValue(memberId, out var memberPre) || !memberPre.IsAlive)
                    {
                        continue;
                    }

                    var dx = (long)memberPre.XRaw - leaderPre.XRaw;
                    var dy = (long)memberPre.YRaw - leaderPre.YRaw;
                    var squared = checked((dx * dx) + (dy * dy));
                    if (squared > spreadSquared)
                    {
                        spreadSquared = squared;
                    }
                }

                // Recomputed independently from the formula design section
                // 3.4 states, not by calling the production helper.
                var windowOpen =
                    ((tick + (slot * rules.CohesionCycleTicks / 16)) % rules.CohesionCycleTicks) <
                    rules.CohesionDutyTicks;
                var state = postTick[leaderId].ContingentState;

                if (spreadSquared > cohesionRadiusSquared &&
                    windowOpen &&
                    state == ContingentState.Advance)
                {
                    fired = true;
                    diagnostics =
                        $"slot {slot} at tick {tick}: spreadSquared {spreadSquared} > " +
                        $"cohesionRadiusSquared {cohesionRadiusSquared}, window open, " +
                        "state Advance.";
                    break;
                }
            }

            preTick = postTick;
        }

        Assert.True(
            fired,
            "Expected the cross-contingent gate to deny cohesion to a " +
            "contingent whose spread otherwise qualifies for Hold. If this " +
            "never triggers, the scenario failed to build the worst case " +
            "and this fact is correct to fail rather than pass vacuously. " +
            diagnostics);
    }

    /// <summary>
    /// The engineered roster for the converging-squares scenario, built
    /// exactly to design section 10.3's worst case rather than an arbitrary
    /// crossing:
    /// <list type="bullet">
    /// <item>one distant enemy cluster shared by both contingents, inside
    /// <see cref="Scenario.PerceptionRangeRaw"/> of both and far enough away
    /// that neither contingent's <c>nearestEnemySquared</c> approaches
    /// <c>closeRadiusRaw^2</c> during the convergence, so <c>Close</c> and
    /// <c>Break</c> stay silent throughout;</item>
    /// <item>both contingents aimed at the same enemy, so both leaders'
    /// directions of travel are broadly the same and each contingent's bias
    /// square trails behind its own leader on the same side;</item>
    /// <item>the two leaders offset laterally on opposite sides of the line
    /// to the enemy, so advancing toward it brings them together rather
    /// than apart;</item>
    /// <item>the lateral offset chosen so the two trail bases start within
    /// <c>aMarginRaw + bMarginRaw</c> of each other on both axes -- squares
    /// overlapping from the first tick, verified below by hand arithmetic
    /// against the exact production formula
    /// (<see cref="BattleSimulation"/>'s <c>ComputeRallyDirection</c> /
    /// <c>ComputeRallyTrailBase</c>), not guessed;</item>
    /// <item>non-leader members placed behind their own leader relative to
    /// the enemy, in the shared trailing region where the two squares
    /// coincide;</item>
    /// <item>a map large enough that neither contingent's bias square can
    /// come within one raw unit of any edge at any point in the run, so
    /// gate 5 provably cannot fire and any denial this scenario observes is
    /// attributable to gate 6 alone.</item>
    /// </list>
    /// At this scenario's body radius (512 raw units, half of
    /// <see cref="FixedPoint.Scale"/>), a four-member contingent gives
    /// <c>jitterRaw = 2,560</c>, <c>trailRaw = 5,376</c> and
    /// <c>marginRaw = 3,072</c>
    /// (<see cref="FormationRules.ComputeContingentJitterRaw"/>,
    /// <see cref="FormationRules.ComputeContingentTrailRaw"/>). With the
    /// leaders 4,000 raw units apart laterally and 300,000 raw units from
    /// the shared enemy, the two trail bases differ by 0 on X and roughly
    /// 4,070 on Y at tick 0 -- well inside the combined margin of 6,144 --
    /// confirmed by hand against the exact leader-direction and
    /// trail-base arithmetic before this file was written.
    /// </summary>
    private static AgentState[] BuildConvergingSquaresRoster(Scenario scenario)
    {
        const int LeaderXRaw = 100_000;
        const int MidlineYRaw = 100_000;
        const int LateralOffsetRaw = 2_000;
        // Comfortably beyond cohesionRadiusRaw (24 * BodyRadiusRaw = 12,288
        // at this scenario's body radius), so this member alone pushes the
        // contingent's spread past rule 5's Hold-entry bar from tick 1 --
        // the "would have qualified for Hold absent gate 6" signature the
        // companion fact needs. Placed west of the leader (behind it,
        // opposite the direction of travel toward the shared enemy), per
        // "non-leader members placed in the trailing region... behind its
        // own leader relative to E".
        const int WideFollowerOffsetRaw = 20_480;
        const int EnemyXRaw = 400_000;

        return
        [
            // Contingent A (contingentId 0), on the -Y side of the midline.
            // The two close fillers sit 1,500 raw units from the leader
            // (well clear of the 1,024-raw-unit body diameter at this
            // scenario's body radius -- a spawn any closer than that
            // overlaps bodies and can jam the collision resolver before the
            // battle even starts) while staying far inside the straggler
            // threshold (9,216), so they contribute nothing to the
            // contingent's spread.
            CreateAgentAtRawPosition(
                2, 0, LeaderXRaw, MidlineYRaw - LateralOffsetRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5,
                0,
                LeaderXRaw - WideFollowerOffsetRaw,
                MidlineYRaw - LateralOffsetRaw,
                scenario,
                contingentId: 0),
            CreateAgentAtRawPosition(
                6,
                0,
                LeaderXRaw + 1_500,
                MidlineYRaw - LateralOffsetRaw + 1_500,
                scenario,
                contingentId: 0),
            CreateAgentAtRawPosition(
                7,
                0,
                LeaderXRaw + 1_500,
                MidlineYRaw - LateralOffsetRaw - 1_500,
                scenario,
                contingentId: 0),

            // Contingent B (contingentId 1), the mirror image on the +Y
            // side -- the lateral offset that makes the two leaders' paths
            // to the same distant enemy cross as both advance.
            CreateAgentAtRawPosition(
                12, 0, LeaderXRaw, MidlineYRaw + LateralOffsetRaw, scenario, contingentId: 1),
            CreateAgentAtRawPosition(
                15,
                0,
                LeaderXRaw - WideFollowerOffsetRaw,
                MidlineYRaw + LateralOffsetRaw,
                scenario,
                contingentId: 1),
            CreateAgentAtRawPosition(
                16,
                0,
                LeaderXRaw + 1_500,
                MidlineYRaw + LateralOffsetRaw + 1_500,
                scenario,
                contingentId: 1),
            CreateAgentAtRawPosition(
                17,
                0,
                LeaderXRaw + 1_500,
                MidlineYRaw + LateralOffsetRaw - 1_500,
                scenario,
                contingentId: 1),

            // The whole opposing faction as a single cluster at one point,
            // jittered by 1,200 raw units (again clear of the body
            // diameter) to avoid an overlapping spawn, so every faction-0
            // member selects the same target from tick 1.
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, MidlineYRaw, scenario),
            CreateAgentAtRawPosition(101, 1, EnemyXRaw, MidlineYRaw + 1_200, scenario),
            CreateAgentAtRawPosition(102, 1, EnemyXRaw, MidlineYRaw - 1_200, scenario),
        ];
    }

    private static Scenario ConvergingSquaresScenario() =>
        new(
            Seed: 1,
            MapWidth: 1_000,
            MapHeight: 400,
            AgentsPerFaction: 4,
            TickRate: 20,
            TickLimit: 20_000)
        {
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            LastStandThresholdAgents = 0,
            MovementPreset = MovementPresetId.PersistentContingentsV2,
        };

    // ------------------------------------------------------------------
    // The crossing-traffic residual (design sections 3.5 and 10.2): an
    // unbounded residual, not a guard. If either fact below fails, that is
    // a design finding against open question 6 (design section 13), not an
    // implementation defect -- no new guard is invented to make it pass.
    // ------------------------------------------------------------------

    /// <summary>
    /// The residual design section 3.5 states honestly rather than argues
    /// away: the cross-contingent gate makes a bias square unshared as an
    /// <b>aim region</b>, and does nothing whatever about bodies standing
    /// in it or walking through it. Two same-faction contingents on the
    /// same heading toward one distant shared enemy, one directly behind
    /// the other: the forward contingent (<c>F</c>) is granted cohesion
    /// (<see cref="ContingentState.Hold"/>), and the rear contingent's
    /// (<c>R</c>) independently-pursuing members are routed, by placement
    /// rather than by steering, straight through <c>F</c>'s granted bias
    /// square. This is the only evidence design section 3.5 has for the
    /// residual; if the fourfold packing margin cannot actually absorb this
    /// traffic, this fact is where that shows up.
    /// </summary>
    [Fact]
    public void IndependentSameFactionTrafficCrossingAGrantedBiasSquareReachesATerminalOutcome()
    {
        var scenario = CrossingTrafficScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario, BuildCrossingTrafficRoster(scenario));

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();
        }

        Assert.True(
            simulation.Tick < scenario.TickLimit,
            "The battle failed to reach a terminal outcome before the " +
            $"{scenario.TickLimit}-tick limit; last outcome was " +
            $"{simulation.Outcome}.");
        Assert.True(
            simulation.Outcome is BattleOutcome.Faction0Victory or BattleOutcome.Faction1Victory,
            $"Expected a decisive outcome, not a forced draw; got {simulation.Outcome}.");
    }

    /// <summary>
    /// The companion fact that keeps the liveness assertion above from
    /// passing because <c>F</c>'s square was never really granted, or was
    /// never really occupied. A recorded <see cref="ContingentState.Hold"/>
    /// is exactly the observable statement "this contingent was granted
    /// cohesion on this tick": rules 1-3 of the state machine would have
    /// written <c>Break</c> or <c>Close</c>, rule 4 would have written
    /// <c>Advance</c> on a shut window, and the resolving stage writes
    /// <c>Advance</c> rather than <c>Hold</c> whenever either geometric gate
    /// denies. <c>Hold</c> is reachable only when every one of those has
    /// been passed. <b>This fact has no guard to disable</b>, and that is
    /// not an oversight -- it does not test a mechanism, it measures
    /// whether an unbounded residual bites. There is no invented switch
    /// here to satisfy the disable-and-fail demonstration the other facts
    /// in this file carry.
    /// </summary>
    [Fact]
    public void TheCrossingTrafficScenarioReallyGrantsCohesionWhileTheSquareIsOccupied()
    {
        var scenario = CrossingTrafficScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario, BuildCrossingTrafficRoster(scenario));
        var forwardMemberIds = new ulong[] { 15, 16, 17, 18 };
        var fMemberIds = new ulong[] { 2, 5, 6, 7 };
        // A game-design threshold, not a measurement: roughly a quarter of
        // R's five living members, chosen so the foreign headcount inside
        // F's square is a material fraction of the packing margin rather
        // than a single stray body. It may need adjusting once this
        // scenario is first run against the real collision resolver; what
        // may not change is the shape -- the fact must fail if the traffic
        // never really entered the square.
        const int RequiredOccupants = 4;

        var preTick = Snapshot(simulation);
        var confirmed = false;
        var bestOccupants = 0;

        while (!confirmed &&
            simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < Math.Min(scenario.TickLimit, 200))
        {
            simulation.AdvanceOneTick();
            var postTick = Snapshot(simulation);

            if (postTick[2].ContingentState == ContingentState.Hold &&
                preTick.TryGetValue(2, out var fLeaderPre) &&
                fLeaderPre.IsAlive &&
                postTick[2].TargetEntityId is { } targetId &&
                preTick.TryGetValue(targetId, out var targetPre))
            {
                var livingCount = fMemberIds.Count(
                    id => preTick.TryGetValue(id, out var a) && a.IsAlive);

                var jitterRaw = FormationRules.ComputeContingentJitterRaw(
                    scenario.BodyRadiusRaw, livingCount);
                var trailRaw = FormationRules.ComputeContingentTrailRaw(
                    scenario.BodyRadiusRaw, jitterRaw);
                var trailBase = ComputeTrailBase(
                    fLeaderPre.XRaw, fLeaderPre.YRaw, targetPre.XRaw, targetPre.YRaw, trailRaw);
                var marginRaw = checked(jitterRaw + scenario.BodyRadiusRaw);

                var occupants = forwardMemberIds.Count(id =>
                    preTick.TryGetValue(id, out var member) &&
                    member.IsAlive &&
                    Math.Abs(member.XRaw - trailBase.XRaw) <= marginRaw &&
                    Math.Abs(member.YRaw - trailBase.YRaw) <= marginRaw);

                if (occupants > bestOccupants)
                {
                    bestOccupants = occupants;
                }

                if (occupants >= RequiredOccupants)
                {
                    confirmed = true;
                }
            }

            preTick = postTick;
        }

        Assert.True(
            confirmed,
            "Expected at least one tick where F was recorded Hold and at " +
            $"least {RequiredOccupants} of R's living non-leader members " +
            $"lay inside F's bias square; the best observed occupancy " +
            $"across the window checked was {bestOccupants}. If this " +
            "never happens, that is a finding against design section 13's " +
            "open question 6, not an implementation defect.");
    }

    /// <summary>
    /// The engineered roster for the crossing-traffic scenario, built to
    /// design section 10.3's worst case:
    /// <list type="bullet">
    /// <item><c>F</c> (contingentId 0) must be granted cohesion, and
    /// provably so: its non-leader members are strung out beyond
    /// <c>cohesionRadiusRaw</c> so rule 5's entry bar is met, it has at
    /// least <see cref="MovementRuleset.MinimumCohesiveMembers"/> living
    /// members so rule 2 cannot fire, and the shared enemy is far enough
    /// away that rule 3 cannot fire either;</item>
    /// <item>gate 6 must provably <b>not</b> fire on the F-R pair -- the
    /// opposite requirement from the converging-squares scenario above:
    /// <c>R</c>'s leader sits far enough behind <c>F</c>'s trail base along
    /// the shared heading that the two trail bases are separated by more
    /// than <c>FMarginRaw + RMarginRaw</c> on that axis;</item>
    /// <item><c>R</c>'s non-leader members are all within the straggler
    /// threshold of <c>R</c>'s own leader, so gate 4 sends every one of
    /// them to independent pursuit and <c>R</c>'s own spread never
    /// approaches <c>cohesionRadiusRaw</c>, so <c>R</c> stays
    /// <see cref="ContingentState.Advance"/>;</item>
    /// <item><c>R</c>'s members are placed forward of <c>R</c>'s leader,
    /// laterally aligned with <c>F</c>'s trail base, so their straight-line
    /// pursuit path to the shared enemy runs through <c>F</c>'s bias
    /// square.</item>
    /// </list>
    /// At this scenario's body radius, <c>F</c> (four living members) and
    /// <c>R</c> (five) both give <c>jitterRaw = 2,560</c>,
    /// <c>trailRaw = 5,376</c> and <c>marginRaw = 3,072</c>
    /// (<see cref="FormationRules.ComputeContingentJitterRaw"/>,
    /// <see cref="FormationRules.ComputeContingentTrailRaw"/>), so the
    /// combined margin is 6,144. The two leaders are placed 8,144 raw units
    /// apart along the heading axis -- 2,000 raw units beyond the combined
    /// margin -- and <c>R</c>'s four forward members, at offsets 1,500 to
    /// 5,700 raw units ahead of <c>R</c>'s leader, all land inside
    /// <c>F</c>'s bias square's X range at tick 0, all comfortably inside
    /// the 9,216-raw-unit straggler threshold. All of this is confirmed by
    /// hand against the exact production trail-base arithmetic before this
    /// file was written, not guessed.
    /// </summary>
    private static AgentState[] BuildCrossingTrafficRoster(Scenario scenario)
    {
        const int MidlineYRaw = 100_000;
        const int FLeaderXRaw = 300_000;
        // FMarginRaw (3,072) + RMarginRaw (3,072) = 6,144, plus 2,000 raw
        // units of slack: strictly beyond the combined margin, so
        // FormationRules.DoCohesionSquaresOverlap denies overlap for the
        // F-R pair and gate 6 provably does not fire on it.
        const int GapRaw = 8_144;
        const int RLeaderXRaw = FLeaderXRaw - GapRaw;
        const int EnemyXRaw = 600_000;
        // Comfortably beyond cohesionRadiusRaw (12,288 at this scenario's
        // body radius: 16,000 diagonal gives spreadSquared 512,000,000), so
        // F's own spread clears rule 5's Hold-entry bar without any help
        // from a gate that would otherwise deny it.
        const int FFarFollowerOffsetRaw = 16_000;
        // R's four forward members: each strictly inside the 9,216-raw-unit
        // straggler threshold of R's own leader, so gate 4 sends every one
        // of them to independent pursuit; each at least 1,400 raw units
        // from its neighbour (clear of the 1,024-raw-unit body diameter, so
        // no spawn overlaps another); and, by the module comment above this
        // method, landing inside F's bias square [291,552, 297,696] on the
        // heading axis.
        int[] forwardOffsetsRaw = [1_500, 2_900, 4_300, 5_700];

        var agents = new List<AgentState>
        {
            // Contingent F (contingentId 0): the forward contingent,
            // granted cohesion. The two close fillers sit 1,500 raw units
            // from the leader, clear of the body diameter.
            CreateAgentAtRawPosition(2, 0, FLeaderXRaw, MidlineYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5,
                0,
                FLeaderXRaw - FFarFollowerOffsetRaw,
                MidlineYRaw - FFarFollowerOffsetRaw,
                scenario,
                contingentId: 0),
            CreateAgentAtRawPosition(
                6, 0, FLeaderXRaw + 1_500, MidlineYRaw + 1_500, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                7, 0, FLeaderXRaw + 1_500, MidlineYRaw - 1_500, scenario, contingentId: 0),

            // Contingent R (contingentId 1): the rear contingent, never
            // granted cohesion, whose independently-pursuing members are
            // routed straight through F's bias square.
            CreateAgentAtRawPosition(12, 0, RLeaderXRaw, MidlineYRaw, scenario, contingentId: 1),
        };

        var nextId = 15UL;
        foreach (var offset in forwardOffsetsRaw)
        {
            agents.Add(
                CreateAgentAtRawPosition(
                    nextId, 0, RLeaderXRaw + offset, MidlineYRaw, scenario, contingentId: 1));
            nextId++;
        }

        agents.Add(CreateAgentAtRawPosition(100, 1, EnemyXRaw, MidlineYRaw, scenario));
        agents.Add(CreateAgentAtRawPosition(101, 1, EnemyXRaw, MidlineYRaw + 1_200, scenario));
        agents.Add(CreateAgentAtRawPosition(102, 1, EnemyXRaw, MidlineYRaw - 1_200, scenario));

        return [.. agents];
    }

    private static Scenario CrossingTrafficScenario() =>
        new(
            Seed: 1,
            MapWidth: 1_200,
            MapHeight: 400,
            AgentsPerFaction: 5,
            TickRate: 20,
            TickLimit: 20_000)
        {
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            LastStandThresholdAgents = 0,
            MovementPreset = MovementPresetId.PersistentContingentsV2,
        };

    // ------------------------------------------------------------------
    // Shape 3, the map-corner pin (design sections 3.5 and 10.2), and the
    // undersized-map total-degradation claim.
    // ------------------------------------------------------------------

    /// <summary>
    /// Failure shape 3: a contingent's leader pinned in a map corner, with
    /// its members behind it, so both axes of
    /// <see cref="CollisionGeometry.ClampCenterToBounds"/> engage at once --
    /// the corner-contact case that method's own remarks describe. This
    /// exercises the map-edge open-ground test (gate 5) in situ, and it
    /// exercises the one residual design section 3.5 states honestly rather
    /// than argues away: the give-way aim point is still clamped, and this
    /// is the test that proves the clamp does not stall a member against a
    /// corner. At this scenario's body radius the leader's unclamped trail
    /// base sits at roughly <c>(-3,289, -3,289)</c> -- confirmed by hand
    /// against the exact production formula -- which is nowhere near the
    /// legal interval on either axis, so gate 5 denies unconditionally
    /// while the leader remains pinned.
    /// </summary>
    [Fact]
    public void AContingentLeaderPinnedInAMapCornerReachesATerminalOutcome()
    {
        var scenario = CornerPinScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario, BuildCornerPinRoster(scenario));

        while (simulation.Outcome == BattleOutcome.Ongoing &&
            simulation.Tick < scenario.TickLimit)
        {
            simulation.AdvanceOneTick();
        }

        Assert.True(
            simulation.Tick < scenario.TickLimit,
            "The battle failed to reach a terminal outcome before the " +
            $"{scenario.TickLimit}-tick limit; last outcome was " +
            $"{simulation.Outcome}.");
        Assert.True(
            simulation.Outcome is BattleOutcome.Faction0Victory or BattleOutcome.Faction1Victory,
            $"Expected a decisive outcome, not a forced draw; got {simulation.Outcome}.");
    }

    private static AgentState[] BuildCornerPinRoster(Scenario scenario)
    {
        var bodyRadiusRaw = scenario.BodyRadiusRaw;
        // The leader sits exactly at the corner of the legal clamp interval
        // (CollisionGeometry.ClampCenterToBounds pulls a coordinate into
        // [bodyRadiusRaw, dimensionRaw - bodyRadiusRaw]), so both axes
        // engage at once.
        var leaderXRaw = bodyRadiusRaw;
        var leaderYRaw = bodyRadiusRaw;
        const int EnemyXRaw = 700_000;
        const int EnemyYRaw = 700_000;
        // Comfortably beyond cohesionRadiusRaw, so this member alone clears
        // rule 5's Hold-entry bar: if gate 5 did not deny, every non-leader
        // living member -- including this one -- would be pulled toward a
        // trail base collapsed against the corner.
        const int FarFollowerOffsetRaw = 16_000;

        return
        [
            CreateAgentAtRawPosition(2, 0, leaderXRaw, leaderYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5,
                0,
                leaderXRaw + FarFollowerOffsetRaw,
                leaderYRaw + FarFollowerOffsetRaw,
                scenario,
                contingentId: 0),
            // The two close fillers: each at least 1,024 raw units (the
            // body diameter at this scenario's body radius) from the
            // leader and from each other, and kept in positive coordinates
            // since the leader itself is pinned at the map's own origin
            // corner.
            CreateAgentAtRawPosition(
                6, 0, leaderXRaw + 1_500, leaderYRaw + 300, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                7, 0, leaderXRaw + 300, leaderYRaw + 1_500, scenario, contingentId: 0),

            CreateAgentAtRawPosition(100, 1, EnemyXRaw, EnemyYRaw, scenario),
            CreateAgentAtRawPosition(101, 1, EnemyXRaw, EnemyYRaw + 1_200, scenario),
            CreateAgentAtRawPosition(102, 1, EnemyXRaw, EnemyYRaw - 1_200, scenario),
        ];
    }

    private static Scenario CornerPinScenario() =>
        new(
            Seed: 1,
            MapWidth: 800,
            MapHeight: 800,
            AgentsPerFaction: 4,
            TickRate: 20,
            TickLimit: 20_000)
        {
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            LastStandThresholdAgents = 0,
            MovementPreset = MovementPresetId.PersistentContingentsV2,
        };

    /// <summary>
    /// The total-degradation claim design section 3.5 makes for the
    /// map-edge test, asserted rather than assumed: on a map too small to
    /// hold any contingent's bias square, <see cref="MovementPresetId.PersistentContingentsV2"/>
    /// produces the identical trajectory <see cref="MovementPresetId.IndependentPursuitV1"/>
    /// would. At this scenario's body radius a four-member contingent (one
    /// living member above <see cref="MovementRuleset.MinimumCohesiveMembers"/>,
    /// needed below so one member can be placed beyond
    /// <c>cohesionRadiusRaw</c> to make gate 5's denial non-vacuous) gives
    /// <c>jitterRaw = 2,560</c> and <c>marginRaw = 3,072</c>, so the
    /// never-fits threshold
    /// (<c>2 * marginRaw = 6,144</c> raw units, per
    /// <see cref="FormationRules.IsCohesionSquareWithinBounds"/>'s own
    /// remarks: "when the map is smaller than this on <b>either</b> axis, no
    /// trail base can satisfy both comparisons on that axis, so this always
    /// reports false") exceeds this scenario's 4,096-raw-unit map
    /// <b>width</b>, regardless of where the trail base falls or what the
    /// map's height is. The height is deliberately generous instead --
    /// 358,400 raw units -- so the roster below has room for a long,
    /// straight run before either side closes to attack range; the gate 5
    /// guarantee
    /// rests on the width alone. Living count can only ever fall toward the
    /// three-member floor over a battle, never rise -- jitter only grows
    /// with living count -- so the map stays undersized for the whole run
    /// no matter what casualties occur.
    /// </summary>
    /// <remarks>
    /// The comparison window is deliberately bounded to the approach phase,
    /// before either simulation's leader comes within
    /// <see cref="Scenario.AttackRangeRaw"/> of the enemy and switches from
    /// <see cref="AgentIntent.Moving"/> to <see cref="AgentIntent.Attacking"/>.
    /// T10's own remarks record that the arrival-slowdown taper of design
    /// section 3.6 is gated on the movement <i>preset</i>, not on whether
    /// cohesion is in play: it applies to every movement kind under
    /// <see cref="MovementPresetId.PersistentContingentsV2"/>, ordinary
    /// pursuit included, so <see cref="MovementPresetId.IndependentPursuitV1"/>
    /// and <c>PersistentContingentsV2</c> are expected to diverge once a
    /// pursuer nears contact -- that divergence is T10's own feature,
    /// already proven correct by <c>ArrivalTaperTests</c>, and is not part
    /// of the map-edge gate's total-degradation claim this fact checks. The
    /// enemy is itself alive and closes on the roster below just as the
    /// roster closes on it, so the two sides approach at roughly twice a
    /// single agent's own movement speed; the roster is placed 300,000 raw
    /// units from the enemy specifically so that even at that combined
    /// rate, sixty ticks leaves comfortably more than
    /// <see cref="Scenario.AttackRangeRaw"/> (12,288 raw units, the
    /// default) of separation remaining, confirmed by hand before this file
    /// was written.
    /// </remarks>
    [Fact]
    public void ACohesionSquareTooLargeForTheMapDegradesToIndependentPursuit()
    {
        const int ComparisonTicks = 60;

        var v2Scenario = DegenerateMapScenario(MovementPresetId.PersistentContingentsV2);
        var v1Scenario = DegenerateMapScenario(MovementPresetId.IndependentPursuitV1);

        var v2 = BattleSimulation.CreateForTesting(v2Scenario, BuildDegenerateMapRoster(v2Scenario));
        var v1 = BattleSimulation.CreateForTesting(v1Scenario, BuildDegenerateMapRoster(v1Scenario));

        for (var i = 0; i < ComparisonTicks; i++)
        {
            Assert.Equal(BattleOutcome.Ongoing, v1.Outcome);
            v1.AdvanceOneTick();
            v2.AdvanceOneTick();

            AssertIdenticalMovementRelevantState(v1, v2);
        }

        Assert.Equal(ComparisonTicks, v1.Tick);
    }

    // Every pairwise separation below is at least 1,024 raw units (the body
    // diameter at this scenario's body radius), so no two agents spawn
    // overlapping. The map's width (4,096 raw units) is what stays under
    // the never-fits threshold; its height is generous so the leader has a
    // long, straight run north to the enemy before either side closes to
    // attack range, all at the same X so no agent ever nears the narrow
    // axis's own edge.
    private static AgentState[] BuildDegenerateMapRoster(Scenario scenario) =>
        [
            CreateAgentAtRawPosition(2, 0, 1_536, 5_000, scenario, contingentId: 0),
            CreateAgentAtRawPosition(5, 0, 1_536, 6_100, scenario, contingentId: 0),
            CreateAgentAtRawPosition(6, 0, 1_536, 3_900, scenario, contingentId: 0),
            // Comfortably beyond cohesionRadiusRaw (12,288), so this member
            // alone clears rule 5's Hold-entry bar -- without this member
            // the contingent's spread never approaches cohesionRadiusRaw and
            // gate 5 would never have anything to deny, which would make
            // this fact vacuous with respect to the mechanism it exists to
            // check.
            CreateAgentAtRawPosition(7, 0, 1_536, 21_000, scenario, contingentId: 0),
            CreateAgentAtRawPosition(100, 1, 1_536, 305_000, scenario),
        ];

    private static Scenario DegenerateMapScenario(MovementPresetId preset) =>
        new(
            Seed: 1,
            MapWidth: 4,
            MapHeight: 350,
            AgentsPerFaction: 3,
            TickRate: 20,
            TickLimit: 1_000)
        {
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            LastStandThresholdAgents = 0,
            MovementPreset = preset,
        };

    /// <summary>
    /// Compares every movement-relevant field two simulations' matching
    /// agents carry, deliberately excluding
    /// <see cref="AgentView.ContingentState"/>:
    /// <see cref="MovementPresetId.PersistentContingentsV2"/> still labels
    /// the contingent (<see cref="ContingentState.Advance"/>, or
    /// <see cref="ContingentState.Break"/> after a casualty) even though the
    /// map-edge gate denies it a cohesion destination on every tick, whereas
    /// <see cref="MovementPresetId.IndependentPursuitV1"/> never writes a
    /// state at all. That labelling difference is expected and is not part
    /// of the total-degradation claim this fact asserts.
    /// </summary>
    private static void AssertIdenticalMovementRelevantState(
        BattleSimulation v1,
        BattleSimulation v2)
    {
        var v1Agents = v1.Agents.ToDictionary(agent => agent.EntityId);
        var v2Agents = v2.Agents.ToDictionary(agent => agent.EntityId);

        Assert.Equal(v1Agents.Count, v2Agents.Count);

        foreach (var (entityId, v1Agent) in v1Agents)
        {
            var v2Agent = v2Agents[entityId];

            Assert.Equal(v1Agent.XRaw, v2Agent.XRaw);
            Assert.Equal(v1Agent.YRaw, v2Agent.YRaw);
            Assert.Equal(v1Agent.HitPoints, v2Agent.HitPoints);
            Assert.Equal(v1Agent.IsAlive, v2Agent.IsAlive);
            Assert.Equal(v1Agent.Intent, v2Agent.Intent);
            Assert.Equal(v1Agent.TargetEntityId, v2Agent.TargetEntityId);
            Assert.Equal(v1Agent.MovementResolution, v2Agent.MovementResolution);
        }
    }

    // ------------------------------------------------------------------
    // Shared helpers.
    // ------------------------------------------------------------------

    private static AgentState CreateAgentAtRawPosition(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        int contingentId = 0) =>
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
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
            contingentId: contingentId);

    private static Dictionary<ulong, AgentView> Snapshot(BattleSimulation simulation) =>
        simulation.Agents.ToDictionary(agent => agent.EntityId);

    /// <summary>
    /// Replicates <see cref="BattleSimulation"/>'s own private
    /// <c>ComputeRallyDirection</c> / <c>ComputeRallyTrailBase</c> pair --
    /// the point <paramref name="trailRaw"/> raw units behind
    /// <paramref name="leaderXRaw"/>/<paramref name="leaderYRaw"/>, opposite
    /// the direction toward <paramref name="targetXRaw"/>/<paramref name="targetYRaw"/> --
    /// so a test can independently recompute what a contingent's bias
    /// square was centred on for a given tick's tick-start positions, the
    /// same way <c>PersistentContingentTests.CohesionObserver</c> does for
    /// its own file.
    /// </summary>
    private static (int XRaw, int YRaw) ComputeTrailBase(
        int leaderXRaw,
        int leaderYRaw,
        int targetXRaw,
        int targetYRaw,
        int trailRaw)
    {
        var deltaXRaw = (long)targetXRaw - leaderXRaw;
        var deltaYRaw = (long)targetYRaw - leaderYRaw;

        if (deltaXRaw == 0 && deltaYRaw == 0)
        {
            return (leaderXRaw, leaderYRaw);
        }

        var distanceRaw = IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));

        var trailXRaw = checked((int)(leaderXRaw - (deltaXRaw * trailRaw / distanceRaw)));
        var trailYRaw = checked((int)(leaderYRaw - (deltaYRaw * trailRaw / distanceRaw)));

        return (trailXRaw, trailYRaw);
    }

    /// <summary>
    /// The same integer square root <see cref="FormationRules"/> and
    /// <see cref="BattleSimulation"/> each carry their own copy of: a binary
    /// digit-by-digit extraction, exact for every non-negative
    /// <see cref="long"/> and requiring no floating point.
    /// </summary>
    private static long IntegerSquareRoot(long value)
    {
        var remainder = checked((ulong)value);
        ulong root = 0;
        var bit = 1UL << 62;

        while (bit > remainder)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (remainder >= root + bit)
            {
                remainder -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }

            bit >>= 2;
        }

        return checked((long)root);
    }
}
