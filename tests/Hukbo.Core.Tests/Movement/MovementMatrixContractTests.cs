using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The whole movement scenario matrix, run once. <see cref="MovementScenarioMatrix"/>
/// enumerates 21 unordered one-versus-one cells and 231 unordered
/// team-versus-team matchups over the six canonical loadouts; this class
/// consumes every one of them and applies the shared movement contract that
/// does not depend on which weapon a cell happens to contain — determinism,
/// step legality, declared phases and postures, lifecycle bounds, caller-order
/// independence, and observable progress.
/// </summary>
/// <remarks>
/// <para>
/// This class replaces three per-weapon slices of the same matrix. The Itak,
/// Kalis, and tall-hardwood-shield sessions each filtered the matrix to the
/// cells containing their own loadout — eleven or twelve of the 21 duels and
/// 176 of the 231 team matchups apiece — and each wrote its own contract over
/// that slice. Every cell therefore ran two or three times, and each run
/// applied whichever third of the union its owning session had implemented.
/// The union is applied here to every cell instead, exactly once:
/// </para>
/// <list type="bullet">
/// <item>
/// the twin-rerun determinism check over the state hash, the ordered event
/// stream, the event hash, and the outcome, across the approved seed set, from
/// the shield session;
/// </item>
/// <item>
/// the per-tick step legality, declared-member, non-negative-timer, and
/// no-progress-streak checks, also from the shield session, whose
/// <c>RunToCompletion</c> helper is the most complete of the three and is
/// carried over here;
/// </item>
/// <item>
/// the caller-order independence check — the same warriors handed to the
/// simulation in reverse order must agree on the state hash every tick — with
/// its pace-ceiling and distinct-ally-position assertions, from the Kalis
/// session;
/// </item>
/// <item>
/// the same-input construction-hash check from the Itak session, which the
/// twin rerun subsumes: two runs that agree on the terminal state hash after
/// advancing necessarily agreed on it at construction.
/// </item>
/// </list>
/// <para>
/// Caller-order independence is a property of the simulation's ordering
/// discipline, not of any particular loadout pairing, so it does not need all
/// 231 team matchups to be exercised. It runs over the 21 one-versus-one cells
/// and the 21 team mirrors, which together cover every canonical loadout in
/// both the two-agent and the four-agent geometry. Running it over every
/// matchup as the Kalis slice did buys combinations of a property that does not
/// vary by combination.
/// </para>
/// <para>
/// The matrix's own combinatorial invariants — the counts, the uniqueness, the
/// canonical enumeration order, the mirror counts, and the shielded-cell
/// flags — belong to <see cref="MovementScenarioMatrixTests"/> and are not
/// restated here. The per-weapon suites each carried a slice-count fact of
/// their own; those facts existed to justify a slice, and the slices are gone.
/// </para>
/// <para>
/// Every threshold, pace, and distance reached through these cells is a
/// <strong>provisional reconstruction: gameplay tuning, not a historical
/// measurement</strong>. No assertion in this class reads a winner. Every
/// scenario names <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> and
/// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> explicitly: V2 is
/// the only combat preset fielding all six canonical loadouts, and V6 is the
/// only movement preset that reads the shielded rows. Neither shipped default
/// is touched.
/// </para>
/// </remarks>
public sealed class MovementMatrixContractTests
{
    // ----- Shared probe constants, matching the foundation derivation -----

    private const int AttackRangeRaw = 5 * FixedPoint.Scale; // 5120

    private const int BodyRadiusRaw = FixedPoint.Scale / 2; // 512

    private const int MovementSpeedRaw = FixedPoint.Scale / 2; // 512

    /// <summary>Half of the 200-cell-wide map, in raw units.</summary>
    private const int MapCenterXRaw = 102_400;

    /// <summary>The mirrored 1v1 and 2v2 x offset from the map centre.</summary>
    private const int MirrorOffsetXRaw = 10_240;

    /// <summary>The shared 1v1 row, the vertical map centre.</summary>
    private const int MirrorYRaw = 51_200;

    /// <summary>The first team member's row in the 2v2 geometry.</summary>
    private const int TeamFirstMemberYRaw = 49_152;

    /// <summary>The second team member's row in the 2v2 geometry.</summary>
    private const int TeamSecondMemberYRaw = 53_248;

    private const int OneVersusOneTicks = 600;

    private const int TeamMatchupTicks = 200;

    /// <summary>
    /// The tick budget of the caller-order checks. Shorter than the replay
    /// budgets because the property under observation — that reversing the
    /// caller's array changes nothing — either holds from the first tick that
    /// two agents contend or does not hold at all.
    /// </summary>
    private const int CallerOrderTicks = 150;

    private static readonly ulong[] ApprovedSeeds = [1, 2, 3, 5, 8];

    private static readonly ulong[] CallerOrderSeeds = [1, 2];

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    private static CombatRuleset CombatRules =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);

    // ----- Cell sources -----

    /// <summary>All 21 unordered one-versus-one cells, unfiltered.</summary>
    public static TheoryData<int, int> OneVersusOneCells()
    {
        var data = new TheoryData<int, int>();
        foreach (var pair in MovementScenarioMatrix.EnumerateOneVersusOnePairs())
        {
            data.Add(pair.FirstLoadoutIndex, pair.SecondLoadoutIndex);
        }

        return data;
    }

    /// <summary>All 231 unordered team-versus-team matchups, unfiltered.</summary>
    public static TheoryData<int, int, int, int> TeamMatchupCells()
    {
        var data = new TheoryData<int, int, int, int>();
        foreach (var matchup in MovementScenarioMatrix.EnumerateTeamMatchups())
        {
            data.Add(
                matchup.FirstTeam.FirstMemberIndex,
                matchup.FirstTeam.SecondMemberIndex,
                matchup.SecondTeam.FirstMemberIndex,
                matchup.SecondTeam.SecondMemberIndex);
        }

        return data;
    }

    /// <summary>
    /// The 21 team mirrors — every canonical two-member composition faced
    /// against a copy of itself. The caller-order checks use this set for
    /// their four-agent geometry: it covers every loadout in every
    /// composition without paying for all 231 matchups of a property that
    /// does not vary by matchup.
    /// </summary>
    public static TheoryData<int, int> TeamMirrorCompositions()
    {
        var data = new TheoryData<int, int>();
        foreach (var team in MovementScenarioMatrix.EnumerateTeamCompositions())
        {
            data.Add(team.FirstMemberIndex, team.SecondMemberIndex);
        }

        return data;
    }

    // ----- The one-versus-one slice: all 21 cells -----

    /// <summary>
    /// Every one-versus-one cell runs a mirrored duel on each of the approved
    /// seeds <c>1, 2, 3, 5, 8</c>, twice per seed from an identical
    /// construction, and the two runs agree on the state hash, the ordered
    /// event stream, the event hash, and the outcome. The per-tick movement
    /// contract holds throughout: no agent exceeds the per-axis speed
    /// baseline, no agent's Euclidean step exceeds the baseline by more than
    /// the documented one-raw-unit integer-lattice truncation headroom, every
    /// footwork phase and posture is a declared member, and no lifecycle timer
    /// goes negative. The isolation invariant is the twin run itself: the
    /// repeat construction is built and advanced after the subject run has
    /// finished, so a cell that leaked state across runs would diverge. The
    /// progress invariant is the no-progress streak — a cell that neither
    /// terminates nor moves nor emits an event for its whole budget is a
    /// defect, not a slow test.
    /// </summary>
    [Theory]
    [MemberData(nameof(OneVersusOneCells))]
    public void EveryOneVersusOneCellReplaysIdenticallyOnEveryApprovedSeed(
        int firstIndex,
        int secondIndex)
    {
        foreach (var seed in ApprovedSeeds)
        {
            var scenario = CreateScenario(seed);
            var cellName = $"{CellName(firstIndex, secondIndex)} seed {seed}";

            AgentState[] Build() =>
                BuildDuel(scenario, firstIndex, secondIndex);

            var run = RunToCompletion(scenario, Build(), OneVersusOneTicks);
            var repeat = RunToCompletion(scenario, Build(), OneVersusOneTicks);

            AssertRunContract(run, repeat, cellName, OneVersusOneTicks);
        }
    }

    /// <summary>
    /// Every one-versus-one cell, on seeds one and two: handing the same two
    /// warriors to the simulation in reverse caller order produces an
    /// identical state hash on every tick, and neither warrior ever retains a
    /// pace above its own forward cap. Incidental call order never decides an
    /// outcome — the determinism contract of
    /// <c>SIMULATION-GAME-STANDARDS.md</c> section 4 — observed at the level
    /// where the caller controls it.
    /// </summary>
    [Theory]
    [MemberData(nameof(OneVersusOneCells))]
    public void EveryOneVersusOneCellIsIndependentOfCallerOrder(
        int firstIndex,
        int secondIndex)
    {
        foreach (var seed in CallerOrderSeeds)
        {
            var scenario = CreateScenario(seed);
            var forward = BuildDuel(scenario, firstIndex, secondIndex);
            var reversed = BuildDuel(scenario, firstIndex, secondIndex);
            Array.Reverse(reversed);

            AssertCallerOrderIndependence(
                scenario,
                forward,
                reversed,
                $"{CellName(firstIndex, secondIndex)} seed {seed}");
        }
    }

    // ----- The team-matchup slice: all 231 cells -----

    /// <summary>
    /// Every team-versus-team matchup runs a mirrored 2v2 twice from an
    /// identical construction and holds the same per-tick movement contract,
    /// determinism, and progress invariants as the one-versus-one slice.
    /// </summary>
    /// <remarks>
    /// This slice runs at seed one only, deliberately, and the reasoning is
    /// the shield session's: the full approved seed set across 231 cells at
    /// two runs per cell would put 2,310 simulations inside one file inside
    /// the canonical gate, which the gate does not need and should not pay
    /// for. The seed sweep is exercised end to end by the 21 one-versus-one
    /// cells above, which do run all five seeds, and the property this slice
    /// adds is combinatorial coverage of team composition rather than seed
    /// coverage.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TeamMatchupCells))]
    public void EveryTeamMatchupCellReplaysIdenticallyAtSeedOne(
        int firstTeamFirstIndex,
        int firstTeamSecondIndex,
        int secondTeamFirstIndex,
        int secondTeamSecondIndex)
    {
        var scenario = CreateScenario();
        var cellName =
            $"{CellName(firstTeamFirstIndex, firstTeamSecondIndex)} vs " +
            CellName(secondTeamFirstIndex, secondTeamSecondIndex);

        AgentState[] Build() => BuildTeamRoster(
            scenario,
            firstTeamFirstIndex,
            firstTeamSecondIndex,
            secondTeamFirstIndex,
            secondTeamSecondIndex);

        var run = RunToCompletion(scenario, Build(), TeamMatchupTicks);
        var repeat = RunToCompletion(scenario, Build(), TeamMatchupTicks);

        AssertRunContract(run, repeat, cellName, TeamMatchupTicks);
    }

    /// <summary>
    /// Every team mirror, on seeds one and two: the four warriors handed to
    /// the simulation in reverse caller order produce an identical state hash
    /// on every tick, no warrior on either side ever retains a pace above its
    /// own forward cap, and two living allies never resolve to the same
    /// position. The four-agent geometry is what makes this distinct from the
    /// one-versus-one case: the conflict pass only has work to do when two
    /// same-faction proposals contend.
    /// </summary>
    [Theory]
    [MemberData(nameof(TeamMirrorCompositions))]
    public void EveryTeamMirrorIsIndependentOfCallerOrder(
        int firstMemberIndex,
        int secondMemberIndex)
    {
        foreach (var seed in CallerOrderSeeds)
        {
            var scenario = CreateScenario(seed);
            var forward = BuildTeamRoster(
                scenario,
                firstMemberIndex,
                secondMemberIndex,
                firstMemberIndex,
                secondMemberIndex);
            var reversed = BuildTeamRoster(
                scenario,
                firstMemberIndex,
                secondMemberIndex,
                firstMemberIndex,
                secondMemberIndex);
            Array.Reverse(reversed);

            AssertCallerOrderIndependence(
                scenario,
                forward,
                reversed,
                $"{CellName(firstMemberIndex, secondMemberIndex)} mirror " +
                $"seed {seed}");
        }
    }

    // ----- Geometry builders -----

    private static AgentState[] BuildDuel(
        Scenario scenario,
        int firstIndex,
        int secondIndex)
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        return
        [
            CreateAgent(
                1,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                MirrorYRaw,
                scenario,
                loadouts[firstIndex]),
            CreateAgent(
                2,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                MirrorYRaw,
                scenario,
                loadouts[secondIndex]),
        ];
    }

    private static AgentState[] BuildTeamRoster(
        Scenario scenario,
        int firstTeamFirstIndex,
        int firstTeamSecondIndex,
        int secondTeamFirstIndex,
        int secondTeamSecondIndex)
    {
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        return
        [
            CreateAgent(
                1,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                TeamFirstMemberYRaw,
                scenario,
                loadouts[firstTeamFirstIndex]),
            CreateAgent(
                2,
                factionId: 0,
                MapCenterXRaw - MirrorOffsetXRaw,
                TeamSecondMemberYRaw,
                scenario,
                loadouts[firstTeamSecondIndex]),
            CreateAgent(
                3,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                TeamFirstMemberYRaw,
                scenario,
                loadouts[secondTeamFirstIndex]),
            CreateAgent(
                4,
                factionId: 1,
                MapCenterXRaw + MirrorOffsetXRaw,
                TeamSecondMemberYRaw,
                scenario,
                loadouts[secondTeamSecondIndex]),
        ];
    }

    /// <summary>
    /// Every scenario names its combat preset and its movement preset
    /// explicitly. <c>PrecolonialPhilippinesV2</c> is the only combat preset
    /// that fields all six canonical loadouts, so it spans the whole matrix,
    /// and <c>EquipmentRelativeFootworkV6</c> is the only movement preset that
    /// reads the shielded rows. Neither shipped default is touched.
    /// </summary>
    private static Scenario CreateScenario(ulong seed = 1) =>
        new(
            Seed: seed,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 5_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = AttackRangeRaw,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = BodyRadiusRaw,
            MovementSpeedRaw = MovementSpeedRaw,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

    /// <summary>
    /// Builds one agent carrying its weapon's real reach, damage, and cooldown
    /// from the named combat preset, exactly as a full-roster run does, so a
    /// matchup between two different weapons is not artificially reach-equal.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout)
    {
        var rules = CombatRules;
        var weapon = rules.HasWeaponProfiles
            ? rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                scenario.DamagePerAttack,
                scenario.AttackRangeRaw,
                scenario.AttackCooldownTicks);

        return new AgentState(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            weapon.AttackRangeRaw,
            weapon.DamagePerAttack,
            weapon.AttackCooldownTicks,
            loadout);
    }

    // ----- Contracts -----

    /// <summary>
    /// The shared per-cell verdict: the twin runs agree bit for bit, every
    /// step was legal, every phase, posture, and timer was declared and
    /// non-negative, and the cell either terminated or made observable
    /// progress rather than stalling for its whole budget.
    /// </summary>
    private static void AssertRunContract(
        RunEvidence run, RunEvidence repeat, string cellName, int tickBudget)
    {
        Assert.True(
            run.StateHash == repeat.StateHash,
            $"Cell {cellName}: the twin rerun diverged on the state hash.");
        Assert.Equal(run.EventStream, repeat.EventStream);
        Assert.True(
            run.EventHash == repeat.EventHash,
            $"Cell {cellName}: the twin rerun diverged on the event hash.");
        Assert.Equal(run.Outcome, repeat.Outcome);
        Assert.True(run.LegalSteps, run.StepFailure ?? $"Cell {cellName}: step.");
        Assert.True(
            run.LegalPhases, run.PhaseFailure ?? $"Cell {cellName}: phase.");
        Assert.True(
            run.Outcome != BattleOutcome.Ongoing ||
            run.MaximumNoProgressStreak < tickBudget,
            $"Cell {cellName}: the battle neither reached an outcome nor " +
            $"progressed — {run.MaximumNoProgressStreak} consecutive ticks " +
            $"of the {tickBudget}-tick budget moved no living agent and " +
            "emitted no event.");
    }

    /// <summary>
    /// Advances two simulations built from the same warriors in opposite
    /// caller order, asserting on every tick that their state hashes agree,
    /// that no warrior retains a pace above its own forward cap, and that no
    /// two living allies occupy the same position.
    /// </summary>
    private static void AssertCallerOrderIndependence(
        Scenario scenario,
        AgentState[] forward,
        AgentState[] reversed,
        string cellName)
    {
        var first = BattleSimulation.CreateForTesting(scenario, forward);
        var second = BattleSimulation.CreateForTesting(scenario, reversed);

        for (var tick = 0; tick < CallerOrderTicks; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();

            Assert.True(
                first.ComputeStateHash() == second.ComputeStateHash(),
                $"Cell {cellName}, tick {first.Tick}: reversing the caller's " +
                "array changed the state hash.");

            AssertPaceCeilingsAndDistinctAllyPositions(
                forward, first, cellName);
        }
    }

    private static void AssertPaceCeilingsAndDistinctAllyPositions(
        AgentState[] agents, BattleSimulation simulation, string cellName)
    {
        foreach (var agent in agents)
        {
            var cap = MovementRouteRules.DesiredPaceRaw(
                MovementSpeedRaw,
                V6.ResolveLoadoutProfile(agent.Loadout)
                    .ForwardPaceBasisPoints);

            Assert.True(
                agent.MovementPaceRaw <= cap,
                $"Cell {cellName}: agent {agent.EntityId} retained pace " +
                $"{agent.MovementPaceRaw} above its own forward cap {cap} " +
                $"on tick {simulation.Tick}.");
        }

        foreach (var agent in agents)
        {
            foreach (var other in agents)
            {
                if (agent.EntityId >= other.EntityId ||
                    agent.FactionId != other.FactionId ||
                    !agent.IsAlive ||
                    !other.IsAlive)
                {
                    continue;
                }

                Assert.False(
                    agent.XRaw == other.XRaw && agent.YRaw == other.YRaw,
                    $"Cell {cellName}: allies {agent.EntityId} and " +
                    $"{other.EntityId} resolved the same position on tick " +
                    $"{simulation.Tick}.");
            }
        }
    }

    /// <summary>
    /// Advances a battle and records the non-authoritative evidence this file
    /// asserts on, without altering a single authoritative field. Carried over
    /// from <c>TallHardwoodMovementScenarioTests.RunToCompletion</c>, which was
    /// the most complete of the three per-weapon copies; the shield-specific
    /// fields it also collected — the refusal streak and the
    /// unconditional-posture flag — stay with the shield suite, which is the
    /// only caller that asserts on them.
    /// </summary>
    private static RunEvidence RunToCompletion(
        Scenario scenario, AgentState[] agents, int ticks)
    {
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var toleratedStepSquared =
            (Int128)(scenario.MovementSpeedRaw + 1) *
            (scenario.MovementSpeedRaw + 1);
        var previous = agents.ToDictionary(
            agent => agent.EntityId, agent => (agent.XRaw, agent.YRaw));
        var eventStream = new List<string>();
        var eventHash = Fnv1a.OffsetBasis;
        var legalSteps = true;
        var legalPhases = true;
        string? stepFailure = null;
        string? phaseFailure = null;
        var maximumNoProgressStreak = 0;
        var noProgressStreak = 0;

        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.AdvanceOneTick();
            var anyAgentMoved = false;

            foreach (var agent in agents)
            {
                if (agent.IsAlive)
                {
                    var (priorX, priorY) = previous[agent.EntityId];
                    var deltaX = (long)agent.XRaw - priorX;
                    var deltaY = (long)agent.YRaw - priorY;
                    var movedSquared =
                        ((Int128)deltaX * deltaX) + ((Int128)deltaY * deltaY);

                    anyAgentMoved |= deltaX != 0 || deltaY != 0;

                    // The shipped step model scales the target delta by
                    // paceRaw divided by a truncated integer square root of
                    // the distance, so the per-axis cap is exact while the
                    // Euclidean magnitude may exceed the cap by less than one
                    // raw unit. Both bounds are asserted, at their real
                    // strengths, exactly as the Kampilan session recorded.
                    if (Math.Abs(deltaX) > scenario.MovementSpeedRaw ||
                        Math.Abs(deltaY) > scenario.MovementSpeedRaw)
                    {
                        legalSteps = false;
                        stepFailure ??=
                            $"Agent {agent.EntityId} moved ({deltaX},{deltaY}) " +
                            $"on tick {tick}, exceeding the per-axis baseline " +
                            $"{scenario.MovementSpeedRaw}.";
                    }

                    if (movedSquared > toleratedStepSquared)
                    {
                        legalSteps = false;
                        stepFailure ??=
                            $"Agent {agent.EntityId} moved ({deltaX},{deltaY}) " +
                            $"on tick {tick}, squared {movedSquared}, beyond " +
                            $"the one-raw-unit truncation tolerance " +
                            $"{toleratedStepSquared}.";
                    }
                }

                previous[agent.EntityId] = (agent.XRaw, agent.YRaw);

                if (!Enum.IsDefined(agent.FootworkPhase) ||
                    !Enum.IsDefined(agent.TacticalPosture) ||
                    agent.FootworkTicksRemaining < 0)
                {
                    legalPhases = false;
                    phaseFailure ??=
                        $"Agent {agent.EntityId} on tick {tick} carried " +
                        $"phase {agent.FootworkPhase}, posture " +
                        $"{agent.TacticalPosture}, timer " +
                        $"{agent.FootworkTicksRemaining}.";
                }
            }

            var anyEvent = false;
            foreach (var battleEvent in simulation.LastEvents)
            {
                anyEvent = true;
                eventStream.Add(
                    $"{battleEvent.Sequence}:{battleEvent.Tick}:" +
                    $"{battleEvent.Kind}:{battleEvent.SourceEntityId}:" +
                    $"{battleEvent.TargetEntityId ?? 0}:{battleEvent.Value}");
                Fnv1a.Add(ref eventHash, (ulong)battleEvent.Sequence);
                Fnv1a.Add(ref eventHash, (ulong)battleEvent.Tick);
                Fnv1a.Add(ref eventHash, (ulong)battleEvent.Kind);
                Fnv1a.Add(ref eventHash, battleEvent.SourceEntityId);
                Fnv1a.Add(ref eventHash, battleEvent.TargetEntityId ?? 0UL);
                Fnv1a.Add(ref eventHash, (ulong)(long)battleEvent.Value);
            }

            if (anyAgentMoved || anyEvent)
            {
                noProgressStreak = 0;
            }
            else
            {
                noProgressStreak++;
                if (noProgressStreak > maximumNoProgressStreak)
                {
                    maximumNoProgressStreak = noProgressStreak;
                }
            }

            if (simulation.Outcome != BattleOutcome.Ongoing)
            {
                break;
            }
        }

        return new RunEvidence(
            simulation.ComputeStateHash(),
            eventStream,
            eventHash,
            simulation.Outcome,
            legalSteps,
            legalPhases,
            stepFailure,
            phaseFailure,
            maximumNoProgressStreak);
    }

    // ----- Helpers -----

    private static string CellName(int firstIndex, int secondIndex) =>
        MovementScenarioMatrix.CanonicalLoadoutCodes[firstIndex] + "-" +
        MovementScenarioMatrix.CanonicalLoadoutCodes[secondIndex];

    private sealed record RunEvidence(
        ulong StateHash,
        List<string> EventStream,
        ulong EventHash,
        BattleOutcome Outcome,
        bool LegalSteps,
        bool LegalPhases,
        string? StepFailure,
        string? PhaseFailure,
        int MaximumNoProgressStreak);
}
