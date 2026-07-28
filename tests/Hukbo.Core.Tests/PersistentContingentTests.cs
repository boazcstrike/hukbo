using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// T11 behavioural and liveness coverage for
/// <see cref="MovementPresetId.PersistentContingentsV2"/>: design section
/// 10.3's proving tests plus the state machine and the cohesion rule,
/// observed through a running <see cref="BattleSimulation"/> rather than
/// through hand-built arguments. The unit-level state-machine, duty-cycle
/// and leader-scan properties live in
/// <c>tests/Hukbo.Core.Tests/ContingentStateMachineTests.cs</c> (T9) and the
/// arrival-taper sweep lives in
/// <c>tests/Hukbo.Core.Tests/ArrivalTaperTests.cs</c> (T10); neither is
/// duplicated here. The two remaining engineered deadlock scenarios (the
/// converging-squares and crossing-traffic residuals, and the map-corner
/// pin) are T12's, not this file's.
/// </summary>
public sealed class PersistentContingentTests
{
    // ------------------------------------------------------------------
    // 1. The twenty-seed liveness sweep (design section 10.3, "Packing
    //    deadlock").
    // ------------------------------------------------------------------

    /// <summary>
    /// Mirrors
    /// <see cref="LastStandFormationTests.NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwenty"/>
    /// under the new preset. This sweep is <b>not</b> the liveness proof for
    /// the two engineered failure geometries of design section 10.2, or for
    /// the crossing-traffic residual of section 3.5 — a sweep that passes
    /// shows only that twenty particular trajectories avoided those shapes.
    /// T12 carries all three as deliberately constructed scenarios.
    /// </summary>
    [Fact]
    public void NoBattleUnderPersistentContingentsStallsAtTheTickLimitAcrossSeedsOneThroughTwenty()
    {
        var stalledSeeds = new List<string>();

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var scenario = PersistentContingentsScenario(seed);
            var simulation = BattleSimulation.Create(scenario);

            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
            }

            if (simulation.Tick < scenario.TickLimit)
            {
                continue;
            }

            var livingFaction0 = simulation.Agents.Count(
                agent => agent.FactionId == 0 && agent.IsAlive);
            var livingFaction1 = simulation.Agents.Count(
                agent => agent.FactionId == 1 && agent.IsAlive);
            stalledSeeds.Add(
                $"seed {seed}: stalled at tick {simulation.Tick} of " +
                $"{scenario.TickLimit}, outcome {simulation.Outcome}, " +
                $"living counts [{livingFaction0}, {livingFaction1}].");
        }

        Assert.True(
            stalledSeeds.Count == 0,
            "The following seeds never reached a terminal outcome before " +
            $"the tick limit under PersistentContingentsV2:\n" +
            string.Join('\n', stalledSeeds));
    }

    // ------------------------------------------------------------------
    // 2. The cohesion duty cycle's hard bound, in two forms (design
    //    section 10.3, "Cohesion never outlives its budget").
    // ------------------------------------------------------------------

    /// <summary>
    /// Neither form here is a liveness proof on its own: the duty cycle
    /// bounds how long the aiming lasts, not whether the movement is
    /// granted. The weaker form matches the observable
    /// <see cref="AgentView.ContingentState"/> directly; the stronger form —
    /// the one escape 4 of design section 10.2 actually asserts — recomputes
    /// the six-gate conjunction from outside, using the exact production
    /// statics (<see cref="MovementRules.IsCohesionEligible"/>,
    /// <see cref="MovementRules.IsCohesionWindowOpen"/>,
    /// <see cref="FormationRules.IsCohesionSquareWithinBounds"/>,
    /// <see cref="FormationRules.DoCohesionSquaresOverlap"/>) against two
    /// consecutive <see cref="AgentView"/> snapshots — the same information a
    /// spectator already has, not a duplicate of the gates' own logic.
    /// </summary>
    [Fact]
    public void CohesionNeverOutlivesItsDutyCycleBudgetAcrossSeedsOneThroughTwenty()
    {
        var rules = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV2);
        var failures = new List<string>();

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var scenario = PersistentContingentsScenario(seed);
            var simulation = BattleSimulation.Create(scenario);
            var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
            var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);

            var holdStreakBySlot = new int[FormationPlanner.MaximumContingents * 2];
            var maxHoldStreakBySlot = new int[FormationPlanner.MaximumContingents * 2];
            var coheringStreakByAgent = new Dictionary<ulong, int>();
            var maxCoheringStreak = 0;

            var preTick = Snapshot(simulation);

            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
                var postTick = Snapshot(simulation);
                var tick = checked((int)simulation.Tick);

                var holdingSlots = new HashSet<int>();
                foreach (var agent in postTick.Values)
                {
                    if (agent.IsAlive && agent.ContingentState == ContingentState.Hold)
                    {
                        holdingSlots.Add(Slot(agent.FactionId, agent.ContingentId));
                    }
                }

                for (var slot = 0; slot < holdStreakBySlot.Length; slot++)
                {
                    holdStreakBySlot[slot] = holdingSlots.Contains(slot)
                        ? holdStreakBySlot[slot] + 1
                        : 0;
                    if (holdStreakBySlot[slot] > maxHoldStreakBySlot[slot])
                    {
                        maxHoldStreakBySlot[slot] = holdStreakBySlot[slot];
                    }
                }

                var result = CohesionObserver.Evaluate(
                    preTick,
                    postTick,
                    tick,
                    rules,
                    scenario.BodyRadiusRaw,
                    mapWidthRaw,
                    mapHeightRaw);

                foreach (var (entityId, coheres) in result.AgentCoheres)
                {
                    var streak = coheres
                        ? coheringStreakByAgent.GetValueOrDefault(entityId) + 1
                        : 0;
                    coheringStreakByAgent[entityId] = streak;
                    if (streak > maxCoheringStreak)
                    {
                        maxCoheringStreak = streak;
                    }
                }

                preTick = postTick;
            }

            var worstHoldStreak = maxHoldStreakBySlot.Max();
            if (worstHoldStreak > rules.CohesionDutyTicks)
            {
                failures.Add(
                    $"seed {seed}: a contingent held cohesion for " +
                    $"{worstHoldStreak} consecutive ticks, exceeding the " +
                    $"{rules.CohesionDutyTicks}-tick duty-cycle budget.");
            }

            if (maxCoheringStreak > rules.CohesionDutyTicks)
            {
                failures.Add(
                    $"seed {seed}: an agent received a cohesion destination " +
                    $"for {maxCoheringStreak} consecutive ticks, exceeding " +
                    $"the {rules.CohesionDutyTicks}-tick duty-cycle budget.");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Duty-cycle budget violations:\n" + string.Join('\n', failures));
    }

    // ------------------------------------------------------------------
    // 3. The inertness bar (design section 10.3, "Cohesion is not
    //    practically inert").
    // ------------------------------------------------------------------

    /// <summary>
    /// Implements design section 10.3's inertness bar exactly: coverage,
    /// persistence and spread, all game-design thresholds rather than
    /// measurements. Ten percent sits deliberately below the duty cycle's
    /// own ceiling of <c>CohesionDutyTicks / CohesionCycleTicks</c> (75%),
    /// so the bar cannot collide with a mechanism working as designed. If
    /// this bar fails, the cause must be established before any number
    /// moves — the first suspect is chain denial across converging
    /// contingents (design section 3.5, "Chain denial, many contingents at
    /// once, and the inertness risk"), whose pre-analysed remedy is
    /// narrowing the cross-contingent scan to contingents that could
    /// actually be granted cohesion. That narrowing is a recorded design
    /// trade-off requiring sign-off (design section 13), not something this
    /// task's remediation authority extends to, so a failure here is
    /// reported with the real measured fractions rather than patched.
    /// </summary>
    /// <remarks>
    /// A contingent's "pre-Close window" (design section 10.3) is measured
    /// here as the run of ticks, starting at tick 1, during which that
    /// contingent's recorded <see cref="ContingentState"/> is neither
    /// <see cref="ContingentState.Close"/> nor <see cref="ContingentState.Break"/>
    /// nor a full wipe-out (<see cref="ContingentState.None"/> after having
    /// had a living member) — the last clause is this test's own
    /// completion of an edge case the design text does not address: once
    /// every member of a contingent is dead, cohesion is structurally
    /// impossible for the rest of the battle, and counting those ticks
    /// against the denominator would understate persistence for no reason
    /// connected to the mechanism under test. "The faction's pre-Close
    /// window" in the spread check is read as the longest of its
    /// contingents' own windows; a cohering tick is counted as falling in
    /// the later half of it when the tick's position within its own
    /// contingent's window is at least half that longest window.
    /// </remarks>
    [Fact]
    public void CohesionCoverageIsNotPracticallyInertAcrossSeedsOneThroughTwenty()
    {
        const double PersistenceThreshold = 0.10;
        const int MinimumCoveredContingents = 2;

        var rules = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV2);
        var failures = new List<string>();

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var scenario = PersistentContingentsScenario(seed);
            var simulation = BattleSimulation.Create(scenario);
            var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
            var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);

            var contingentsByFaction = simulation.Agents
                .GroupBy(agent => agent.FactionId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(agent => agent.ContingentId)
                        .Distinct()
                        .ToList());

            var windowIsOpen = new Dictionary<(int Faction, int Contingent), bool>();
            var windowLength = new Dictionary<(int Faction, int Contingent), int>();
            var coheringTicks = new Dictionary<(int Faction, int Contingent), int>();
            var everCohered = new HashSet<(int Faction, int Contingent)>();
            var lastCoheringRelativeTick = new Dictionary<(int Faction, int Contingent), int>();
            var hadALivingMember = new HashSet<(int Faction, int Contingent)>();

            foreach (var (faction, contingents) in contingentsByFaction)
            {
                foreach (var contingent in contingents)
                {
                    windowIsOpen[(faction, contingent)] = true;
                    windowLength[(faction, contingent)] = 0;
                    coheringTicks[(faction, contingent)] = 0;
                }
            }

            var preTick = Snapshot(simulation);

            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
                var postTick = Snapshot(simulation);
                var tick = checked((int)simulation.Tick);

                var result = CohesionObserver.Evaluate(
                    preTick,
                    postTick,
                    tick,
                    rules,
                    scenario.BodyRadiusRaw,
                    mapWidthRaw,
                    mapHeightRaw);

                var stateByContingent = new Dictionary<(int, int), ContingentState>();
                var livingByContingent = new Dictionary<(int, int), int>();
                foreach (var agent in postTick.Values)
                {
                    if (!agent.IsAlive)
                    {
                        continue;
                    }

                    var key = (agent.FactionId, agent.ContingentId);
                    stateByContingent[key] = agent.ContingentState;
                    livingByContingent[key] =
                        livingByContingent.GetValueOrDefault(key) + 1;
                }

                foreach (var key in windowIsOpen.Keys.ToList())
                {
                    if (!windowIsOpen[key])
                    {
                        continue;
                    }

                    var living = livingByContingent.GetValueOrDefault(key);
                    if (living > 0)
                    {
                        hadALivingMember.Add(key);
                    }

                    var state = stateByContingent.GetValueOrDefault(key, ContingentState.None);
                    var wipedOut = state == ContingentState.None &&
                        living == 0 &&
                        hadALivingMember.Contains(key);

                    if (state is ContingentState.Close or ContingentState.Break || wipedOut)
                    {
                        windowIsOpen[key] = false;
                        continue;
                    }

                    windowLength[key]++;

                    var slot = Slot(key.Item1, key.Item2);
                    if (result.SlotCoheres.GetValueOrDefault(slot))
                    {
                        coheringTicks[key]++;
                        everCohered.Add(key);
                        lastCoheringRelativeTick[key] = windowLength[key];
                    }
                }

                preTick = postTick;
            }

            foreach (var (faction, contingents) in contingentsByFaction)
            {
                if (contingents.Count == 0)
                {
                    continue;
                }

                var coveredCount = contingents.Count(
                    contingent => everCohered.Contains((faction, contingent)));
                var requiredCoverage = Math.Max(
                    MinimumCoveredContingents,
                    contingents.Count / 2);
                if (coveredCount < requiredCoverage)
                {
                    failures.Add(
                        $"seed {seed} faction {faction}: coverage {coveredCount} " +
                        $"of {contingents.Count} contingents ever cohered, " +
                        $"required at least {requiredCoverage}.");
                }

                var totalPreCloseTicks = contingents.Sum(
                    contingent => windowLength[(faction, contingent)]);
                var totalCoheringTicks = contingents.Sum(
                    contingent => coheringTicks[(faction, contingent)]);
                var persistence = totalPreCloseTicks == 0
                    ? 0.0
                    : (double)totalCoheringTicks / totalPreCloseTicks;
                if (persistence < PersistenceThreshold)
                {
                    failures.Add(
                        $"seed {seed} faction {faction}: persistence " +
                        $"{totalCoheringTicks}/{totalPreCloseTicks} = " +
                        $"{persistence:P1}, required at least " +
                        $"{PersistenceThreshold:P0}.");
                }

                var longestWindow = contingents.Max(
                    contingent => windowLength[(faction, contingent)]);
                var laterHalfThreshold = longestWindow / 2;
                var spreadSatisfied = contingents.Any(contingent =>
                    lastCoheringRelativeTick.TryGetValue(
                        (faction, contingent),
                        out var relativeTick) &&
                    relativeTick >= laterHalfThreshold);
                if (!spreadSatisfied)
                {
                    failures.Add(
                        $"seed {seed} faction {faction}: no cohering tick " +
                        "fell in the later half of the faction's pre-Close " +
                        $"window (length {longestWindow}) — coverage looks " +
                        "like a burst confined to deployment.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Inertness bar failures — these are game-design thresholds, not " +
            "measurements; per design section 10.3 the cause must be " +
            "established (chain denial is the first suspect) before any " +
            "threshold moves:\n" + string.Join('\n', failures));
    }

    // ------------------------------------------------------------------
    // 4. Chain denial from genuine pairwise overlap (design section 3.5,
    //    "Chain denial, many contingents at once, and the inertness
    //    risk").
    // ------------------------------------------------------------------

    /// <summary>
    /// Three contingents of one faction whose bias squares stand in the
    /// relation A overlaps B, B overlaps C, A disjoint from C — computed
    /// from <see cref="FormationRules.ComputeContingentJitterRaw"/> and
    /// <see cref="FormationRules.ComputeContingentTrailRaw"/>, not guessed
    /// distances. Pins that each denial is its own pairwise fact rather
    /// than a propagation step: A is denied because A genuinely overlaps B,
    /// C is denied because C genuinely overlaps B, and B is denied because
    /// it overlaps both — never because a third contingent's denial
    /// "spread" to it.
    /// </summary>
    [Fact]
    public void ChainDenialArisesFromGenuinePairwiseOverlapNotFromPropagation()
    {
        var scenario = ContingentUnitScenario();
        var bodyRadiusRaw = scenario.BodyRadiusRaw;

        // Three living members per contingent: enough to clear
        // MinimumCohesiveMembers (3) with no deaths, so none of the three
        // ever reaches Break on attrition.
        var jitterRaw = FormationRules.ComputeContingentJitterRaw(bodyRadiusRaw, 3);
        var trailRaw = FormationRules.ComputeContingentTrailRaw(bodyRadiusRaw, jitterRaw);
        var marginRaw = checked(jitterRaw + bodyRadiusRaw);

        // Every leader shares the same X and targets an enemy directly
        // east, level with its own Y, so every leader's direction of
        // travel is exactly +X and every trail base sits exactly trailRaw
        // west of its leader with no rounding: distanceRaw == deltaX
        // exactly.
        const int LeaderXRaw = 500_000;
        const int BigDx = 400_000;

        var yA = 0;
        var yB = checked((int)(1.5 * marginRaw));
        var yC = checked(3 * marginRaw);

        // Construction sanity: A-B and B-C touch or overlap; A-C does not.
        Assert.True(Math.Abs(yA - yB) <= 2 * marginRaw);
        Assert.True(Math.Abs(yB - yC) <= 2 * marginRaw);
        Assert.True(Math.Abs(yA - yC) > 2 * marginRaw);

        var trailBaseXRaw = checked(LeaderXRaw - trailRaw);
        Assert.False(
            FormationRules.DoCohesionSquaresOverlap(
                trailBaseXRaw, yA, marginRaw,
                trailBaseXRaw, yC, marginRaw),
            "Construction error: A and C's bias squares must be disjoint.");
        Assert.True(
            FormationRules.DoCohesionSquaresOverlap(
                trailBaseXRaw, yA, marginRaw,
                trailBaseXRaw, yB, marginRaw),
            "Construction error: A and B's bias squares must overlap.");
        Assert.True(
            FormationRules.DoCohesionSquaresOverlap(
                trailBaseXRaw, yB, marginRaw,
                trailBaseXRaw, yC, marginRaw),
            "Construction error: B and C's bias squares must overlap.");

        var agents = new List<AgentState>();
        AddContingent(agents, contingentId: 0, leaderId: 2, yA, LeaderXRaw, BigDx, scenario);
        AddContingent(agents, contingentId: 1, leaderId: 12, yB, LeaderXRaw, BigDx, scenario);
        AddContingent(agents, contingentId: 2, leaderId: 22, yC, LeaderXRaw, BigDx, scenario);

        var simulation = BattleSimulation.CreateForTesting(scenario, [.. agents]);

        var rules = MovementPresetRegistry.Get(MovementPresetId.PersistentContingentsV2);
        Assert.True(
            MovementRules.IsCohesionWindowOpen(1, 0, rules.CohesionCycleTicks, rules.CohesionDutyTicks));
        Assert.True(
            MovementRules.IsCohesionWindowOpen(1, 1, rules.CohesionCycleTicks, rules.CohesionDutyTicks));
        Assert.True(
            MovementRules.IsCohesionWindowOpen(1, 2, rules.CohesionCycleTicks, rules.CohesionDutyTicks));

        simulation.AdvanceOneTick();

        var stateA = AgentByEntityId(simulation, 2).ContingentState;
        var stateB = AgentByEntityId(simulation, 12).ContingentState;
        var stateC = AgentByEntityId(simulation, 22).ContingentState;

        // Every one of the three spread far enough that, absent gate 6,
        // rule 5 would have selected Hold (spreadSquared clears
        // cohesionRadiusRaw^2 comfortably). Advance, not Hold, is the
        // observable signature of a geometric denial.
        Assert.Equal(ContingentState.Advance, stateA);
        Assert.Equal(ContingentState.Advance, stateB);
        Assert.Equal(ContingentState.Advance, stateC);

        static void AddContingent(
            List<AgentState> agents,
            int contingentId,
            ulong leaderId,
            int y,
            int leaderX,
            int bigDx,
            Scenario scenario)
        {
            agents.Add(CreateAgentAtRawPosition(leaderId, 0, leaderX, y, scenario, contingentId));
            // The straggler: far enough from the leader that spreadSquared
            // strictly exceeds cohesionRadiusRaw^2.
            agents.Add(
                CreateAgentAtRawPosition(
                    checked(leaderId + 1), 0, leaderX, checked(y + (40 * scenario.BodyRadiusRaw)),
                    scenario, contingentId));
            // A filler, kept close to the leader, purely to reach the
            // three-member floor so attrition never fires.
            agents.Add(
                CreateAgentAtRawPosition(
                    checked(leaderId + 2), 0, checked(leaderX + 10), checked(y + 10),
                    scenario, contingentId));
            agents.Add(
                CreateAgentAtRawPosition(
                    checked(leaderId + 100), 1, checked(leaderX + bigDx), y, scenario));
        }
    }

    // ------------------------------------------------------------------
    // 5. The straggler gate, in three forms (design section 3.4, "The
    //    straggler threshold"; section 3.5, gate 4).
    // ------------------------------------------------------------------

    /// <summary>
    /// Inside the threshold and far from its own enemy target (so the
    /// arrival taper of design section 3.6 never engages), a member in
    /// <see cref="ContingentState.Advance"/> proposes the byte-identical
    /// destination it would propose under
    /// <see cref="MovementPresetId.IndependentPursuitV1"/>.
    /// </summary>
    [Fact]
    public void AnAdvanceMemberInsideTheStragglerThresholdMovesIdenticallyToIndependentPursuit()
    {
        const int LeaderXRaw = 1_000_000;
        const int LeaderYRaw = 1_000_000;
        const int EnemyXRaw = LeaderXRaw + 400_000;
        // 5,000 raw units: comfortably inside the 9,216-raw-unit straggler
        // threshold at this scenario's body radius (18 * 512).
        const int MemberOffsetRaw = 5_000;

        var v1Scenario = ContingentUnitScenario() with
        {
            MovementPreset = MovementPresetId.IndependentPursuitV1,
        };
        var v2Scenario = ContingentUnitScenario();

        AgentState[] BuildRoster(Scenario scenario) =>
        [
            CreateAgentAtRawPosition(2, 0, LeaderXRaw, LeaderYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5, 0, checked(LeaderXRaw + MemberOffsetRaw), LeaderYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(9, 0, LeaderXRaw + 10, LeaderYRaw + 10, scenario, contingentId: 0),
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, LeaderYRaw, scenario),
        ];

        var v1 = BattleSimulation.CreateForTesting(v1Scenario, BuildRoster(v1Scenario));
        var v2 = BattleSimulation.CreateForTesting(v2Scenario, BuildRoster(v2Scenario));

        v1.AdvanceOneTick();
        v2.AdvanceOneTick();

        var memberUnderV1 = AgentByEntityId(v1, 5);
        var memberUnderV2 = AgentByEntityId(v2, 5);

        Assert.Equal(memberUnderV1.XRaw, memberUnderV2.XRaw);
        Assert.Equal(memberUnderV1.YRaw, memberUnderV2.YRaw);
    }

    /// <summary>
    /// Beyond the threshold, a member in <see cref="ContingentState.Advance"/>
    /// is pulled toward its own contingent's trail — the opposite of the
    /// ordinary pursuit direction toward its own enemy target — rather than
    /// taking independent pursuit.
    /// </summary>
    [Fact]
    public void AnAdvanceMemberBeyondTheStragglerThresholdIsPulledTowardTheTrailInstead()
    {
        const int LeaderXRaw = 1_000_000;
        const int LeaderYRaw = 1_000_000;
        const int EnemyXRaw = LeaderXRaw + 400_000;
        // 10,000 raw units: strictly beyond the 9,216-raw-unit straggler
        // threshold, but strictly inside cohesionRadiusRaw (12,288 raw
        // units), so entering Hold from None (spreadSquared >
        // cohesionRadiusRaw^2) does not fire either — the contingent stays
        // Advance and gate 4 alone decides this member's fate.
        const int MemberOffsetRaw = 10_000;

        var scenario = ContingentUnitScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, 0, LeaderXRaw, LeaderYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5, 0, LeaderXRaw, checked(LeaderYRaw + MemberOffsetRaw), scenario, contingentId: 0),
            CreateAgentAtRawPosition(9, 0, LeaderXRaw + 10, LeaderYRaw + 10, scenario, contingentId: 0),
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, LeaderYRaw, scenario));

        simulation.AdvanceOneTick();

        var member = AgentByEntityId(simulation, 5);
        Assert.Equal(ContingentState.Advance, AgentByEntityId(simulation, 2).ContingentState);
        Assert.True(
            member.XRaw < LeaderXRaw,
            "Expected the straggling member to be pulled toward the " +
            $"contingent's trail (X below {LeaderXRaw}), which sits behind " +
            "the leader, rather than toward its own enemy target (which " +
            $"would increase X). X was {member.XRaw}.");
    }

    /// <summary>
    /// Pins which side of the strict inequality the straggler boundary
    /// falls on: a member at exactly
    /// <c>16 * memberSquared == 9 * cohesionRadiusRaw^2</c> is not
    /// straggling and takes independent pursuit; one raw unit further out,
    /// it is.
    /// </summary>
    [Fact]
    public void TheStragglerBoundaryIsPinnedAtExactlyEighteenBodyRadii()
    {
        const int LeaderXRaw = 1_000_000;
        const int LeaderYRaw = 1_000_000;
        const int EnemyXRaw = LeaderXRaw + 400_000;

        var scenario = ContingentUnitScenario();
        // 18 * BodyRadiusRaw exactly: 16 * (18R)^2 == 9 * (24R)^2 for every
        // body radius, so this holds regardless of the scenario's own value.
        var boundaryOffsetRaw = checked(18 * scenario.BodyRadiusRaw);

        var atBoundary = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, 0, LeaderXRaw, LeaderYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5, 0, LeaderXRaw, checked(LeaderYRaw + boundaryOffsetRaw), scenario, contingentId: 0),
            CreateAgentAtRawPosition(9, 0, LeaderXRaw + 10, LeaderYRaw + 10, scenario, contingentId: 0),
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, LeaderYRaw, scenario));
        var pastBoundary = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, 0, LeaderXRaw, LeaderYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5, 0, LeaderXRaw, checked(LeaderYRaw + boundaryOffsetRaw + 1), scenario, contingentId: 0),
            CreateAgentAtRawPosition(9, 0, LeaderXRaw + 10, LeaderYRaw + 10, scenario, contingentId: 0),
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, LeaderYRaw, scenario));

        atBoundary.AdvanceOneTick();
        pastBoundary.AdvanceOneTick();

        var memberAtBoundary = AgentByEntityId(atBoundary, 5);
        var memberPastBoundary = AgentByEntityId(pastBoundary, 5);

        Assert.True(
            memberAtBoundary.XRaw > LeaderXRaw,
            "At exactly the boundary, the member is not straggling and " +
            "should take independent pursuit toward the enemy (X above " +
            $"{LeaderXRaw}). X was {memberAtBoundary.XRaw}.");
        Assert.True(
            memberPastBoundary.XRaw < LeaderXRaw,
            "One raw unit past the boundary, the member is straggling and " +
            "should be pulled toward the trail (X below " +
            $"{LeaderXRaw}). X was {memberPastBoundary.XRaw}.");
    }

    // ------------------------------------------------------------------
    // 6. The give-way rule for a contingent leader (design section 3.5,
    //    "The give-way rule").
    // ------------------------------------------------------------------

    /// <summary>
    /// Mirrors
    /// <see cref="LastStandFormationTests.AFollowerStandingInItsLeadersPathStepsAsideRatherThanThroughIt"/>
    /// for a contingent leader rather than the faction rally agent.
    /// </summary>
    [Fact]
    public void AMemberStandingInItsLeadersPathStepsAsideRatherThanThroughIt()
    {
        const int LeaderXRaw = 1_000_000;
        const int LeaderYRaw = 1_000_000;
        const int EnemyXRaw = LeaderXRaw + 200_000;
        // Far ahead (well past the straggler threshold, so cohesion
        // applies) and inside the give-way corridor (2 * BodyRadiusRaw =
        // 1,024 raw units) laterally.
        const int MemberForwardOffsetRaw = 15_360;
        const int MemberLateralOffsetRaw = 600;

        var scenario = ContingentUnitScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, 0, LeaderXRaw, LeaderYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5,
                0,
                checked(LeaderXRaw + MemberForwardOffsetRaw),
                checked(LeaderYRaw + MemberLateralOffsetRaw),
                scenario,
                contingentId: 0),
            CreateAgentAtRawPosition(9, 0, LeaderXRaw + 10, LeaderYRaw + 10, scenario, contingentId: 0),
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, LeaderYRaw, scenario));

        simulation.AdvanceOneTick();

        var after = AgentByEntityId(simulation, 5);
        Assert.NotEqual(MovementResolution.Blocked, after.MovementResolution);

        // Forward projection unchanged: the give-way step has no component
        // along the leader's direction of travel (here, raw X).
        Assert.Equal(checked(LeaderXRaw + MemberForwardOffsetRaw), after.XRaw);

        // Left the corridor centre further than it started: the give-way
        // step moves purely along the perpendicular, away from the axis.
        var lateralBefore = MemberLateralOffsetRaw;
        var lateralAfter = after.YRaw - LeaderYRaw;
        Assert.True(
            lateralAfter > lateralBefore,
            "Expected the member to move further from the leader's axis " +
            $"(lateral before {lateralBefore}, after {lateralAfter}).");
    }

    /// <summary>
    /// Mirrors
    /// <see cref="LastStandFormationTests.TheGiveWaySideIsStableWhenAFollowerIsExactlyOnTheLeadersAxis"/>
    /// for a contingent leader: a member exactly on the leader's own axis
    /// of travel must break the give-way tie the same way regardless of
    /// storage order.
    /// </summary>
    [Fact]
    public void TheGiveWaySideIsStableWhenAMemberIsExactlyOnTheLeadersAxis()
    {
        const int LeaderXRaw = 1_000_000;
        const int LeaderYRaw = 1_000_000;
        const int EnemyXRaw = LeaderXRaw + 200_000;
        const int MemberForwardOffsetRaw = 15_360;

        var scenario = ContingentUnitScenario();

        AgentState Leader() =>
            CreateAgentAtRawPosition(2, 0, LeaderXRaw, LeaderYRaw, scenario, contingentId: 0);
        AgentState Member() =>
            CreateAgentAtRawPosition(
                5, 0, checked(LeaderXRaw + MemberForwardOffsetRaw), LeaderYRaw, scenario, contingentId: 0);
        AgentState Filler() =>
            CreateAgentAtRawPosition(9, 0, LeaderXRaw + 10, LeaderYRaw + 10, scenario, contingentId: 0);
        AgentState Enemy() =>
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, LeaderYRaw, scenario);

        var orderingA = BattleSimulation.CreateForTesting(
            scenario, Leader(), Member(), Filler(), Enemy());
        var orderingB = BattleSimulation.CreateForTesting(
            scenario, Enemy(), Member(), Filler(), Leader());
        var orderingC = BattleSimulation.CreateForTesting(
            scenario, Member(), Enemy(), Leader(), Filler());

        orderingA.AdvanceOneTick();
        orderingB.AdvanceOneTick();
        orderingC.AdvanceOneTick();

        var afterA = AgentByEntityId(orderingA, 5);
        var afterB = AgentByEntityId(orderingB, 5);
        var afterC = AgentByEntityId(orderingC, 5);

        Assert.Equal((afterA.XRaw, afterA.YRaw), (afterB.XRaw, afterB.YRaw));
        Assert.Equal((afterA.XRaw, afterA.YRaw), (afterC.XRaw, afterC.YRaw));

        // Ties break toward the fixed "+" perpendicular. For this purely
        // +X direction of travel that perpendicular step subtracts, so Y
        // must move below the axis, regardless of array order.
        Assert.True(
            afterA.YRaw < LeaderYRaw,
            $"Expected the tie-break to move the member's Y below the axis " +
            $"({LeaderYRaw}), but it was {afterA.YRaw}.");
    }

    // ------------------------------------------------------------------
    // 7. Leader identity, through a running simulation (design section
    //    3.3/3.5).
    // ------------------------------------------------------------------

    /// <summary>
    /// The lowest living <see cref="AgentView.EntityId"/> in a contingent is
    /// exempt from cohesion (gate 2) and keeps ordinary pursuit; every other
    /// living member, straggling, is pulled toward the trail instead. This
    /// is distinct from
    /// <see cref="ContingentStateMachineTests.TheLeaderIsTheLowestLivingEntityIdInItsContingent"/>,
    /// which proves the scan computes the right answer; this proves the
    /// stage actually calls it and acts on what it returns. Selection is
    /// unchanged across three storage permutations of an identical roster.
    /// </summary>
    [Fact]
    public void TheLowestLivingEntityIdIsExemptFromCohesionAcrossStoragePermutations()
    {
        const int LeaderXRaw = 1_000_000;
        const int LeaderYRaw = 1_000_000;
        const int EnemyXRaw = LeaderXRaw + 400_000;
        const int FollowerOffsetRaw = 10_000;

        var scenario = ContingentUnitScenario();

        // Entity 2 is the lowest ID and must be exempt; 5 and 9 are far
        // enough (10,000+ raw units) to straggle and must be pulled. Entity
        // 9's much larger separation keeps it straggling even after one
        // tick's worth of drift, which a small separation would not survive.
        const int FollowerBOffsetRaw = 60_000;
        AgentState Leader() =>
            CreateAgentAtRawPosition(2, 0, LeaderXRaw, LeaderYRaw, scenario, contingentId: 0);
        AgentState FollowerA() =>
            CreateAgentAtRawPosition(
                5, 0, LeaderXRaw, checked(LeaderYRaw + FollowerOffsetRaw), scenario, contingentId: 0);
        AgentState FollowerB() =>
            CreateAgentAtRawPosition(
                9,
                0,
                checked(LeaderXRaw + FollowerBOffsetRaw),
                checked(LeaderYRaw + FollowerOffsetRaw),
                scenario,
                contingentId: 0);
        AgentState Enemy() =>
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, LeaderYRaw, scenario);

        var orderingA = BattleSimulation.CreateForTesting(
            scenario, Leader(), FollowerA(), FollowerB(), Enemy());
        var orderingB = BattleSimulation.CreateForTesting(
            scenario, Enemy(), FollowerB(), FollowerA(), Leader());
        var orderingC = BattleSimulation.CreateForTesting(
            scenario, FollowerA(), Enemy(), Leader(), FollowerB());

        orderingA.AdvanceOneTick();
        orderingB.AdvanceOneTick();
        orderingC.AdvanceOneTick();

        foreach (var ordering in new[] { orderingA, orderingB, orderingC })
        {
            var leader = AgentByEntityId(ordering, 2);
            var followerA = AgentByEntityId(ordering, 5);
            var followerB = AgentByEntityId(ordering, 9);

            Assert.True(
                leader.XRaw > LeaderXRaw,
                "Expected the exempt leader (entity 2) to pursue its own " +
                $"enemy target (X above {LeaderXRaw}). X was {leader.XRaw}.");
            Assert.True(
                followerA.XRaw < LeaderXRaw,
                "Expected the straggling follower (entity 5) to be pulled " +
                $"toward the trail (X below {LeaderXRaw}). X was {followerA.XRaw}.");
            Assert.True(
                followerB.XRaw < LeaderXRaw + FollowerBOffsetRaw,
                "Expected the straggling follower (entity 9) to be pulled " +
                "toward the trail, moving west from its own spawn X " +
                $"({LeaderXRaw + FollowerBOffsetRaw}). X was {followerB.XRaw}.");
        }

        Assert.Equal(
            (AgentByEntityId(orderingA, 2).XRaw, AgentByEntityId(orderingA, 2).YRaw),
            (AgentByEntityId(orderingB, 2).XRaw, AgentByEntityId(orderingB, 2).YRaw));
        Assert.Equal(
            (AgentByEntityId(orderingA, 2).XRaw, AgentByEntityId(orderingA, 2).YRaw),
            (AgentByEntityId(orderingC, 2).XRaw, AgentByEntityId(orderingC, 2).YRaw));
        Assert.Equal(
            (AgentByEntityId(orderingA, 5).XRaw, AgentByEntityId(orderingA, 5).YRaw),
            (AgentByEntityId(orderingB, 5).XRaw, AgentByEntityId(orderingB, 5).YRaw));
        Assert.Equal(
            (AgentByEntityId(orderingA, 5).XRaw, AgentByEntityId(orderingA, 5).YRaw),
            (AgentByEntityId(orderingC, 5).XRaw, AgentByEntityId(orderingC, 5).YRaw));
    }

    /// <summary>
    /// Mirrors <see cref="LastStandFormationTests.RallyAgentDeathPromotesTheNextLowestLivingEntityId"/>:
    /// killing the current leader between ticks promotes the next-lowest
    /// living <see cref="AgentView.EntityId"/>, observed through the same
    /// exemption signature the previous fact establishes.
    /// </summary>
    [Fact]
    public void TheLeaderIsPromotedToTheNextLowestLivingEntityIdOnDeathThroughARunningSimulation()
    {
        const int LeaderXRaw = 1_000_000;
        const int LeaderYRaw = 1_000_000;
        const int EnemyXRaw = LeaderXRaw + 400_000;
        const int FollowerOffsetRaw = 10_000;

        var scenario = ContingentUnitScenario();
        var leaderState = CreateAgentAtRawPosition(2, 0, LeaderXRaw, LeaderYRaw, scenario, contingentId: 0);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            leaderState,
            CreateAgentAtRawPosition(
                5, 0, LeaderXRaw, checked(LeaderYRaw + FollowerOffsetRaw), scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                9,
                0,
                // A much larger separation than entity 5's, so entity 9
                // stays straggling relative to whichever leader is current
                // even after one tick's drift toward the trail has narrowed
                // the gap.
                checked(LeaderXRaw + 60_000),
                checked(LeaderYRaw + FollowerOffsetRaw),
                scenario,
                contingentId: 0),
            // A fourth, filler member kept close to the leader's own spawn:
            // without it, killing entity 2 would drop living membership to
            // two, below MinimumCohesiveMembers (3), and attrition would
            // break the contingent instead of exercising promotion.
            CreateAgentAtRawPosition(
                15, 0, checked(LeaderXRaw - 5_000), LeaderYRaw, scenario, contingentId: 0),
            CreateAgentAtRawPosition(100, 1, EnemyXRaw, LeaderYRaw, scenario));

        simulation.AdvanceOneTick();

        // Confirm the starting exemption signature before killing entity 2.
        Assert.True(AgentByEntityId(simulation, 2).XRaw > LeaderXRaw);
        Assert.True(AgentByEntityId(simulation, 5).XRaw < LeaderXRaw);

        var entity5XBeforePromotion = AgentByEntityId(simulation, 5).XRaw;
        var entity9XBeforePromotion = AgentByEntityId(simulation, 9).XRaw;

        leaderState.HitPoints = 0;

        simulation.AdvanceOneTick();

        Assert.Equal(AgentIntent.Dead, AgentByEntityId(simulation, 2).Intent);

        // Entity 5, now the lowest living EntityId, is promoted and should
        // be exempt this tick: its own pursuit toward the enemy should carry
        // it forward (X increases from where it started this tick), while
        // entity 9 remains a straggler, now pulled toward entity 5's own
        // trail instead (X decreases from where it started this tick).
        Assert.True(
            AgentByEntityId(simulation, 5).XRaw > entity5XBeforePromotion,
            "Expected the promoted leader (entity 5) to keep pursuing its " +
            $"own enemy target (X above {entity5XBeforePromotion}), but X " +
            $"was {AgentByEntityId(simulation, 5).XRaw}.");
        Assert.True(
            AgentByEntityId(simulation, 9).XRaw < entity9XBeforePromotion,
            "Expected the still-straggling follower (entity 9) to be pulled " +
            $"toward the newly promoted leader's trail (X below " +
            $"{entity9XBeforePromotion}), but X was " +
            $"{AgentByEntityId(simulation, 9).XRaw}.");
    }

    // ------------------------------------------------------------------
    // 8. Living count never increases (design section 10.3, "Monotone
    //    attrition").
    // ------------------------------------------------------------------

    [Fact]
    public void LivingCountsNeverIncreaseAcrossAWholeBattleUnderPersistentContingents()
    {
        var scenario = PersistentContingentsScenario(seed: 1);
        var simulation = BattleSimulation.Create(scenario);
        var previousLivingCounts = new[] { int.MaxValue, int.MaxValue };

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            var livingCounts = new[]
            {
                simulation.Agents.Count(agent => agent.FactionId == 0 && agent.IsAlive),
                simulation.Agents.Count(agent => agent.FactionId == 1 && agent.IsAlive),
            };

            for (var faction = 0; faction < 2; faction++)
            {
                Assert.True(
                    livingCounts[faction] <= previousLivingCounts[faction],
                    $"Faction {faction}'s living count rose from " +
                    $"{previousLivingCounts[faction]} to " +
                    $"{livingCounts[faction]} at tick {simulation.Tick}.");
            }

            previousLivingCounts = livingCounts;
        }

        Assert.NotEqual(BattleOutcome.Ongoing, simulation.Outcome);
    }

    // ------------------------------------------------------------------
    // 9. Order independence (design section 10.3, "Order independence").
    // ------------------------------------------------------------------

    /// <summary>
    /// Mirrors <see cref="DeterminismTests.InputArrayOrderCannotChangeOrderedResults"/>
    /// under the persistent-contingent preset, with several contingents per
    /// faction so the leader scan, the spread accumulation and both
    /// geometric gates are all exercised.
    /// </summary>
    [Fact]
    public void InputArrayOrderCannotChangeOrderedResultsUnderPersistentContingents()
    {
        var scenario = OrderIndependenceScenario();
        var ascending = BattleSimulation.CreateForTesting(
            scenario, BuildOrderIndependenceRoster(scenario, AgentOrder.Ascending));
        var descending = BattleSimulation.CreateForTesting(
            scenario, BuildOrderIndependenceRoster(scenario, AgentOrder.Descending));
        var interleaved = BattleSimulation.CreateForTesting(
            scenario, BuildOrderIndependenceRoster(scenario, AgentOrder.Interleaved));

        for (var tick = 0;
            tick < 300 && ascending.Outcome == BattleOutcome.Ongoing;
            tick++)
        {
            ascending.AdvanceOneTick();
            descending.AdvanceOneTick();
            interleaved.AdvanceOneTick();

            AssertSameOrderedResults(ascending, descending, "descending");
            AssertSameOrderedResults(ascending, interleaved, "interleaved");
        }

        Assert.True(ascending.Tick > 0, "The comparison never advanced a tick.");
    }

    private enum AgentOrder
    {
        Ascending,
        Descending,
        Interleaved,
    }

    private static AgentState[] BuildOrderIndependenceRoster(Scenario scenario, AgentOrder order)
    {
        var agents = new List<AgentState>();

        // Two contingents per faction, three members each, spread enough
        // that the state machine and both geometric gates all have
        // something to do.
        for (var faction = 0; faction < 2; faction++)
        {
            var baseX = faction == 0 ? 200_000 : 1_800_000;
            var direction = faction == 0 ? 1 : -1;
            var enemyX = checked(baseX + (direction * 1_500_000));

            for (var contingent = 0; contingent < 2; contingent++)
            {
                var laneY = 200_000 + (contingent * 400_000);
                var leaderEntityId = checked((ulong)((faction * 100) + (contingent * 10) + 1));

                agents.Add(
                    CreateAgentAtRawPosition(
                        leaderEntityId, faction, baseX, laneY, scenario, contingentId: contingent));
                agents.Add(
                    CreateAgentAtRawPosition(
                        checked(leaderEntityId + 1),
                        faction,
                        checked(baseX + (direction * 20_000)),
                        checked(laneY + 15_000),
                        scenario,
                        contingentId: contingent));
                agents.Add(
                    CreateAgentAtRawPosition(
                        checked(leaderEntityId + 2),
                        faction,
                        checked(baseX + (direction * 20_000)),
                        checked(laneY - 15_000),
                        scenario,
                        contingentId: contingent));
            }

            agents.Add(CreateAgentAtRawPosition(
                checked((ulong)(1000 + faction)), faction, enemyX, laneYFor(faction), scenario));
        }

        return order switch
        {
            AgentOrder.Ascending => [.. agents.OrderBy(agent => agent.EntityId)],
            AgentOrder.Descending => [.. agents.OrderByDescending(agent => agent.EntityId)],
            _ => [.. agents],
        };

        static int laneYFor(int faction) => 200_000;
    }

    private static void AssertSameOrderedResults(
        BattleSimulation reference,
        BattleSimulation candidate,
        string orderName)
    {
        if (!reference.Agents.SequenceEqual(candidate.Agents))
        {
            Assert.Fail(
                $"The {orderName} storage order first produced different " +
                $"agent state at tick {reference.Tick}.");
        }

        if (!reference.LastEvents.SequenceEqual(candidate.LastEvents))
        {
            Assert.Fail(
                $"The {orderName} storage order first produced a different " +
                $"event stream at tick {reference.Tick}.");
        }

        var referenceHash = reference.ComputeStateHash();
        var candidateHash = candidate.ComputeStateHash();
        if (referenceHash != candidateHash)
        {
            Assert.Fail(
                $"The {orderName} storage order first produced a different " +
                $"state hash at tick {reference.Tick}: 0x{referenceHash:X16} " +
                $"against 0x{candidateHash:X16}.");
        }
    }

    // ------------------------------------------------------------------
    // 10. Each of the six transition rules, observed through a running
    //     simulation (design section 3.4).
    // ------------------------------------------------------------------

    /// <summary>
    /// Rule 1 (Break is terminal) and rule 2 (attrition breaks), observed
    /// together: forcing a contingent below <c>MinimumCohesiveMembers</c>
    /// selects <see cref="ContingentState.Break"/>, and it stays
    /// <see cref="ContingentState.Break"/> on every later tick this fact
    /// checks. The strict priority order between all six rules — which one
    /// wins when several would otherwise fire on the same inputs — is
    /// pinned directly against <see cref="MovementRules.ResolveContingentState"/>
    /// in <c>ContingentStateMachineTests</c> (T9); a running simulation
    /// cannot be steered into those exact competing-rule input
    /// combinations, so it is not re-asserted here.
    /// </summary>
    [Fact]
    public void AttritionBreakIsObservedAndStaysBrokenThroughARunningSimulation()
    {
        var scenario = ContingentUnitScenario();
        var followerA = CreateAgentAtRawPosition(5, 0, 1_000_010, 1_000_010, scenario, contingentId: 0);
        var followerB = CreateAgentAtRawPosition(9, 0, 1_000_020, 1_000_020, scenario, contingentId: 0);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, 0, 1_000_000, 1_000_000, scenario, contingentId: 0),
            followerA,
            followerB,
            CreateAgentAtRawPosition(100, 1, 40_000_000, 1_000_000, scenario));

        simulation.AdvanceOneTick();
        Assert.NotEqual(ContingentState.Break, AgentByEntityId(simulation, 2).ContingentState);

        // Living count drops from 3 to 1, below MinimumCohesiveMembers (3).
        followerA.HitPoints = 0;
        followerB.HitPoints = 0;

        simulation.AdvanceOneTick();
        Assert.Equal(ContingentState.Break, AgentByEntityId(simulation, 2).ContingentState);

        simulation.AdvanceOneTick();
        Assert.Equal(
            ContingentState.Break,
            AgentByEntityId(simulation, 2).ContingentState);
    }

    /// <summary>Rule 3: an enemy inside <c>closeRadiusRaw</c> selects Close.</summary>
    [Fact]
    public void CloseOnContactIsObservedThroughARunningSimulation()
    {
        var scenario = ContingentUnitScenario();
        // closeRadiusRaw = 16 * BodyRadiusRaw = 8,192 raw units at this
        // scenario's body radius; 2,000 is comfortably inside it.
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, 0, 1_000_000, 1_000_000, scenario, contingentId: 0),
            CreateAgentAtRawPosition(5, 0, 1_000_010, 1_000_010, scenario, contingentId: 0),
            CreateAgentAtRawPosition(9, 0, 1_000_020, 1_000_020, scenario, contingentId: 0),
            CreateAgentAtRawPosition(100, 1, 1_002_000, 1_000_000, scenario));

        simulation.AdvanceOneTick();

        Assert.Equal(ContingentState.Close, AgentByEntityId(simulation, 2).ContingentState);
    }

    /// <summary>
    /// Rules 4, 5 and 6 together, using a contingent with no enemy in
    /// perception range so no agent ever proposes movement and the
    /// geometry stays exactly as placed, tick after tick: rule 6
    /// ("otherwise, Advance") when the contingent is tight; rule 5 (Hold)
    /// once it is spread and the duty-cycle window is open; and rule 4 (a
    /// shut window forces Advance even though the spread has not changed)
    /// once the tick reaches this slot's closed portion of the cycle.
    /// </summary>
    [Fact]
    public void TheGatheringAndShutWindowRulesAreObservedThroughARunningSimulation()
    {
        var scenario = ContingentUnitScenario() with
        {
            TickLimit = 500,
        };

        // Slot 0 (faction 0, contingent 0): phase 0, window open for
        // ticks 0-179 of every 240-tick cycle and shut for 180-239.
        var tightSimulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, 0, 1_000_000, 1_000_000, scenario, contingentId: 0),
            CreateAgentAtRawPosition(5, 0, 1_000_010, 1_000_010, scenario, contingentId: 0),
            CreateAgentAtRawPosition(9, 0, 1_000_020, 1_000_020, scenario, contingentId: 0),
            CreateAgentAtRawPosition(200, 1, 900_000_000, 1_000_000, scenario));

        tightSimulation.AdvanceOneTick();
        Assert.Equal(ContingentState.Advance, AgentByEntityId(tightSimulation, 2).ContingentState);

        // 40 * BodyRadiusRaw: comfortably beyond cohesionRadiusRaw
        // (24 * BodyRadiusRaw), so the entry test for Hold is satisfied.
        var spreadOffsetRaw = checked(40 * scenario.BodyRadiusRaw);
        var spreadSimulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgentAtRawPosition(2, 0, 1_000_000, 1_000_000, scenario, contingentId: 0),
            CreateAgentAtRawPosition(
                5, 0, 1_000_000, checked(1_000_000 + spreadOffsetRaw), scenario, contingentId: 0),
            CreateAgentAtRawPosition(9, 0, 1_000_010, 1_000_010, scenario, contingentId: 0),
            CreateAgentAtRawPosition(200, 1, 900_000_000, 1_000_000, scenario));

        while (spreadSimulation.Tick < 200)
        {
            spreadSimulation.AdvanceOneTick();

            if (spreadSimulation.Tick == 1)
            {
                Assert.Equal(
                    ContingentState.Hold,
                    AgentByEntityId(spreadSimulation, 2).ContingentState);
            }
        }

        // Tick 200 falls at 200 % 240 = 200, inside the closed portion of
        // slot 0's window (180-239): the spread has not changed, but the
        // shut window forces Advance regardless.
        Assert.Equal(
            ContingentState.Advance,
            AgentByEntityId(spreadSimulation, 2).ContingentState);
    }

    // ------------------------------------------------------------------
    // 11. The provisional maximum blocked-streak bound (design section
    //     10.3, "Blocked-streak bound").
    // ------------------------------------------------------------------

    /// <summary>
    /// PROVISIONAL tuning bound, not a measured property of the collision
    /// resolver — matches the shape of
    /// <see cref="LastStandFormationTests.AMaximumSizedLastStandNeverLeavesAWarriorBlockedTooLongAcrossSeedsOneThroughTwenty"/>.
    /// </summary>
    [Fact]
    public void NoAgentIsBlockedTooLongUnderPersistentContingentsAcrossSeedsOneThroughTwenty()
    {
        const int MaximumAllowedBlockedStreakTicks = 200;
        var worstStreakTicks = 0;
        var worstDiagnostics = string.Empty;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var scenario = PersistentContingentsScenario(seed);
            var simulation = BattleSimulation.Create(scenario);

            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
            }

            if (simulation.LongestBlockedStreakTicks <= worstStreakTicks)
            {
                continue;
            }

            worstStreakTicks = simulation.LongestBlockedStreakTicks;
            worstDiagnostics =
                $"seed {seed} stopped at tick {simulation.Tick} of " +
                $"{scenario.TickLimit}, outcome {simulation.Outcome}";
        }

        Assert.True(
            worstStreakTicks <= MaximumAllowedBlockedStreakTicks,
            $"Longest observed blocked streak was {worstStreakTicks} ticks " +
            $"across seeds 1 to 20, exceeding the " +
            $"{MaximumAllowedBlockedStreakTicks}-tick PROVISIONAL bound. " +
            $"Worst seed: {worstDiagnostics}.");
    }

    // ------------------------------------------------------------------
    // Shared scenario, agent and snapshot helpers.
    // ------------------------------------------------------------------

    private static Scenario PersistentContingentsScenario(ulong seed) =>
        Scenario.CreateDefault(seed, totalAgents: 200) with
        {
            MovementPreset = MovementPresetId.PersistentContingentsV2,
        };

    /// <summary>
    /// Shared by hand-built, single-tick or few-tick unit-flavoured facts:
    /// a body radius and movement speed matching
    /// <c>LastStandFormationTests.CreateTestScenario</c>, so the geometric
    /// constants this file's comments reason about (18R straggler
    /// threshold, 24R cohesion radius, 16R close radius) are round numbers.
    /// </summary>
    private static Scenario ContingentUnitScenario() =>
        new(
            Seed: 1,
            MapWidth: 200_000,
            MapHeight: 200_000,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 900_000 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
            LastStandThresholdAgents = 0,
            MovementPreset = MovementPresetId.PersistentContingentsV2,
        };

    private static Scenario OrderIndependenceScenario() =>
        new(
            Seed: 3,
            MapWidth: 4_000,
            MapHeight: 4_000,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 2_000)
        {
            MaximumHitPoints = 100,
            DamagePerAttack = 10,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            PerceptionRangeRaw = 4_000 * FixedPoint.Scale,
            BodyRadiusRaw = CollisionRules.DefaultBodyRadiusRaw,
            MovementSpeedRaw = 3 * FixedPoint.Scale,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            MovementPreset = MovementPresetId.PersistentContingentsV2,
        };

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

    private static AgentView AgentByEntityId(BattleSimulation simulation, ulong entityId) =>
        Assert.Single(simulation.Agents, agent => agent.EntityId == entityId);

    private static Dictionary<ulong, AgentView> Snapshot(BattleSimulation simulation) =>
        simulation.Agents.ToDictionary(agent => agent.EntityId);

    private static int Slot(int factionId, int contingentId) =>
        (factionId * FormationPlanner.MaximumContingents) + contingentId;

    /// <summary>
    /// Test-side observer that determines, for every living agent and every
    /// contingent, whether a cohesion destination was actually granted this
    /// tick. This is <b>not</b> a reimplementation of the six gates' logic:
    /// <see cref="MovementRules.IsCohesionEligible"/>,
    /// <see cref="MovementRules.IsCohesionWindowOpen"/>,
    /// <see cref="FormationRules.IsCohesionSquareWithinBounds"/> and
    /// <see cref="FormationRules.DoCohesionSquaresOverlap"/> are the exact
    /// production statics, called here with inputs reconstructed from two
    /// consecutive <see cref="AgentView"/> snapshots — the same information
    /// a spectator already has. Only the per-tick bookkeeping
    /// <c>BattleSimulation.ResolveContingentStates</c> performs internally
    /// (the leader scan, the trail base, the margin) is replicated, because
    /// no public surface exposes it directly.
    /// </summary>
    private static class CohesionObserver
    {
        internal readonly record struct TickResult(
            IReadOnlyDictionary<int, bool> SlotCoheres,
            IReadOnlyDictionary<ulong, bool> AgentCoheres);

        internal static TickResult Evaluate(
            IReadOnlyDictionary<ulong, AgentView> preTick,
            IReadOnlyDictionary<ulong, AgentView> postTick,
            int tick,
            MovementRuleset rules,
            int bodyRadiusRaw,
            int mapWidthRaw,
            int mapHeightRaw)
        {
            var slotCount = FormationPlanner.MaximumContingents * 2;
            var leaderEntityId = new ulong[slotCount];
            var livingCount = new int[slotCount];

            foreach (var agent in preTick.Values)
            {
                if (!agent.IsAlive)
                {
                    continue;
                }

                var slot = Slot(agent.FactionId, agent.ContingentId);
                livingCount[slot]++;
                if (leaderEntityId[slot] == 0 || agent.EntityId < leaderEntityId[slot])
                {
                    leaderEntityId[slot] = agent.EntityId;
                }
            }

            var trailBaseX = new int[slotCount];
            var trailBaseY = new int[slotCount];
            var margin = new int[slotCount];
            var squareFitsMap = new bool[slotCount];
            var squareOverlapsAnother = new bool[slotCount];

            for (var slot = 0; slot < slotCount; slot++)
            {
                if (livingCount[slot] == 0)
                {
                    continue;
                }

                var leaderId = leaderEntityId[slot];
                var leaderPre = preTick[leaderId];
                var leaderPost = postTick[leaderId];

                var jitterRaw = FormationRules.ComputeContingentJitterRaw(
                    bodyRadiusRaw, livingCount[slot]);
                var trailRaw = FormationRules.ComputeContingentTrailRaw(bodyRadiusRaw, jitterRaw);

                long deltaX = 0;
                long deltaY = 0;
                long distanceRaw = 0;
                if (leaderPost.TargetEntityId is { } targetId &&
                    preTick.TryGetValue(targetId, out var targetPre))
                {
                    deltaX = (long)targetPre.XRaw - leaderPre.XRaw;
                    deltaY = (long)targetPre.YRaw - leaderPre.YRaw;
                    distanceRaw = IntegerSquareRoot(
                        checked((deltaX * deltaX) + (deltaY * deltaY)));
                }

                int tbx;
                int tby;
                if (distanceRaw == 0)
                {
                    tbx = leaderPre.XRaw;
                    tby = leaderPre.YRaw;
                }
                else
                {
                    tbx = checked((int)(leaderPre.XRaw - (deltaX * trailRaw / distanceRaw)));
                    tby = checked((int)(leaderPre.YRaw - (deltaY * trailRaw / distanceRaw)));
                }

                trailBaseX[slot] = tbx;
                trailBaseY[slot] = tby;
                margin[slot] = checked(jitterRaw + bodyRadiusRaw);
                squareFitsMap[slot] = FormationRules.IsCohesionSquareWithinBounds(
                    tbx, tby, jitterRaw, bodyRadiusRaw, mapWidthRaw, mapHeightRaw);
            }

            for (var faction = 0; faction < 2; faction++)
            {
                var baseSlot = faction * FormationPlanner.MaximumContingents;
                for (var outer = 0; outer < FormationPlanner.MaximumContingents; outer++)
                {
                    var outerSlot = baseSlot + outer;
                    if (livingCount[outerSlot] == 0)
                    {
                        continue;
                    }

                    for (var inner = outer + 1; inner < FormationPlanner.MaximumContingents; inner++)
                    {
                        var innerSlot = baseSlot + inner;
                        if (livingCount[innerSlot] == 0)
                        {
                            continue;
                        }

                        if (!FormationRules.DoCohesionSquaresOverlap(
                            trailBaseX[outerSlot], trailBaseY[outerSlot], margin[outerSlot],
                            trailBaseX[innerSlot], trailBaseY[innerSlot], margin[innerSlot]))
                        {
                            continue;
                        }

                        squareOverlapsAnother[outerSlot] = true;
                        squareOverlapsAnother[innerSlot] = true;
                    }
                }
            }

            var slotCoheres = new Dictionary<int, bool>();
            var agentCoheres = new Dictionary<ulong, bool>();

            foreach (var (entityId, agentPre) in preTick)
            {
                if (!agentPre.IsAlive)
                {
                    continue;
                }

                var slot = Slot(agentPre.FactionId, agentPre.ContingentId);
                var agentPost = postTick[entityId];

                if (agentPost.Intent != AgentIntent.Moving || agentPost.TargetEntityId is null)
                {
                    agentCoheres[entityId] = false;
                    continue;
                }

                var isLeader = entityId == leaderEntityId[slot];
                var windowOpen = MovementRules.IsCohesionWindowOpen(
                    tick, slot, rules.CohesionCycleTicks, rules.CohesionDutyTicks);

                var straggling = false;
                if (agentPost.ContingentState == ContingentState.Advance)
                {
                    var leaderPre = preTick[leaderEntityId[slot]];
                    var dx = (long)agentPre.XRaw - leaderPre.XRaw;
                    var dy = (long)agentPre.YRaw - leaderPre.YRaw;
                    var memberSquared = checked((dx * dx) + (dy * dy));
                    var cohesionRadiusRaw = checked(
                        (long)rules.CohesionRadiusMultiplier * bodyRadiusRaw);
                    straggling = checked(16 * memberSquared) >
                        checked(9 * cohesionRadiusRaw * cohesionRadiusRaw);
                }

                var eligible = MovementRules.IsCohesionEligible(
                    agentPost.ContingentState,
                    isLeader,
                    windowOpen,
                    straggling,
                    squareFitsMap[slot],
                    squareOverlapsAnother[slot]);

                agentCoheres[entityId] = eligible;
                if (eligible)
                {
                    slotCoheres[slot] = true;
                }
            }

            return new TickResult(slotCoheres, agentCoheres);
        }

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
}
