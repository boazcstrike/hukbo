using System.Collections.Immutable;

using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The tall-hardwood-shield slice of the movement scenario matrix, run as
/// whole battles. The matrix itself (<see cref="MovementScenarioMatrix"/>)
/// only enumerates cells; this class consumes the shielded ones: the eleven
/// shield-containing unordered 1v1 pairs run as mirrored duels across the
/// approved seed set under a twin-rerun determinism check, the 176
/// shield-containing team matchups run as mirrored 2v2 battles at seed one,
/// and the focused geometries, group cases, asymmetric counts, and roster
/// preservation cases pin the movement shape of the shielded Kalis
/// (<c>KS</c>) and shielded Itak (<c>IS</c>) rows.
/// </summary>
/// <remarks>
/// <para>
/// Every threshold, pace, distance, and count boundary consumed here is a
/// <strong>provisional reconstruction: gameplay tuning; no historical
/// measurement</strong>. The evidence ledger is
/// docs/research/movement/tall-hardwood-shield.md, and the values are owned
/// by <see cref="TallHardwoodMovementProfiles"/>, which this class only
/// reads.
/// </para>
/// <para>
/// Every simulation names <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>
/// and <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> explicitly:
/// V2 is the only combat preset fielding both shielded loadouts, and V6 is
/// the only movement preset that reads the shield rows. Neither shipped
/// default is changed anywhere in this file.
/// </para>
/// <para>
/// No assertion in this class reads a winner. Matchup cells assert the
/// movement contract — determinism, step legality, declared phases, spacing,
/// clearance, and lifecycle bounds — never the outcome. Win rates, equal win
/// rates, universal shield dominance, and the absence of a viable shieldless
/// entry are outcome statistics; they are calibration evidence for the
/// orchestrator's benchmark runs, not pass criteria, and they are deliberately
/// absent here.
/// </para>
/// <para>
/// The weapon plan's 100v100 and 250v250 cases are mass workloads. They belong
/// to <c>scripts/benchmark.ps1</c> and the orchestrator's calibration
/// evidence, not to a unit test inside the canonical gate. The Kampilan
/// session recorded exactly this decision in
/// <c>KampilanMovementTests.AKampilanGroupScenarioStaysDeterministicAndBounded</c>;
/// this class follows it.
/// </para>
/// </remarks>
public sealed class TallHardwoodMovementScenarioTests
{
    // ----- Index spaces: the movement canonical order, not the combat roster -----

    /// <summary>
    /// The shielded Kalis position in the <em>movement</em> canonical order
    /// <c>KP, WA, KA, IT, KS, IS</c>. This is the index
    /// <see cref="MovementRuleset.LoadoutMovementProfiles"/> and the six
    /// opponent-distance offset cells use. It is <strong>not</strong> the
    /// combat roster slot.
    /// </summary>
    private const int ShieldedKalisMovementIndex = 4;

    /// <summary>
    /// The shielded Itak position in the movement canonical order.
    /// </summary>
    private const int ShieldedItakMovementIndex = 5;

    private const int SoloKampilanMovementIndex = 0;

    private const int SoloWasayMovementIndex = 1;

    private const int SoloKalisMovementIndex = 2;

    private const int SoloItakMovementIndex = 3;

    /// <summary>
    /// The shielded Kalis slot in <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>'s
    /// combat roster, which is ordered weapon-first and solo before paired
    /// within a weapon: <c>KP, WA, KA, KS, IT, IS</c>. Shielded Kalis is slot
    /// three here while it is movement index four, and
    /// <see cref="Scenario.RosterCounts"/> is indexed by this roster. A
    /// movement index is never reused as a roster slot.
    /// </summary>
    private const int ShieldedKalisRosterSlot = 3;

    /// <summary>
    /// The shielded Itak slot in the V2 combat roster. Slot five in both
    /// index spaces, which is a coincidence of the two orderings rather than
    /// a shared rule.
    /// </summary>
    private const int ShieldedItakRosterSlot = 5;

    /// <summary>The number of entries in the V2 combat roster.</summary>
    private const int VTwoRosterLength = 6;

    // ----- Shared probe constants, matching the foundation derivation -----

    private const int AttackRangeRaw = 5 * FixedPoint.Scale; // 5120

    private const int BodyRadiusRaw = FixedPoint.Scale / 2; // 512

    private const int MovementSpeedRaw = FixedPoint.Scale / 2; // 512

    /// <summary>
    /// The cooldown value written onto an observed agent before each tick so
    /// no accepted attack can open a commitment and mask the footwork
    /// transition under observation. The same device the Itak session used.
    /// </summary>
    private const int CooldownPin = 100;

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
    /// The eleven shield-containing unordered 1v1 cells, and the 176
    /// shield-containing team matchups, as
    /// <c>MovementScenarioMatrixTests.OneVersusOnePairs_FlagExactlyTheShieldedCellsAsRequiringV2</c>
    /// and its team counterpart already count them from the other side.
    /// </summary>
    private const int ShieldOneVersusOneCellCount = 11;

    private const int ShieldTeamMatchupCellCount = 176;

    private static readonly ulong[] ApprovedSeeds = [1, 2, 3, 5, 8];

    private static readonly CombatLoadout ShieldedKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout ShieldedItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout SoloKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout SoloItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout SoloKampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout SoloWasay =
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    private static CombatRuleset CombatRules =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);

    // ----- The shielded 1v1 slice: eleven of the twenty-one cells -----

    /// <summary>
    /// Of the 21 unordered 1v1 cells, exactly eleven contain a shielded
    /// loadout. Four unshielded loadouts give <c>C(4,2) + 4 = 10</c>
    /// unshielded cells, so <c>21 - 10 = 11</c>. <c>KS-IS</c> is one unordered
    /// cell, not two, which is why the count is eleven rather than twelve.
    /// The slice is asserted for its count, its uniqueness, its two shielded
    /// mirrors, and its canonical enumeration order before any simulation
    /// runs, and it is selected through the matrix's own
    /// <c>RequiresPrecolonialPhilippinesV2</c> predicate rather than a
    /// hand-rolled index check.
    /// </summary>
    [Fact]
    public void TheShieldOneVersusOneSliceCountsElevenUniqueCellsInCanonicalOrder()
    {
        var all = MovementScenarioMatrix.EnumerateOneVersusOnePairs();
        var cells = all
            .Where(pair => pair.RequiresPrecolonialPhilippinesV2)
            .ToList();

        Assert.Equal(ShieldOneVersusOneCellCount, cells.Count);
        Assert.Equal(ShieldOneVersusOneCellCount, cells.Distinct().Count());

        // Both shielded mirrors are present, and exactly one KS-IS cell.
        Assert.Contains(cells, pair =>
            pair.IsMirror &&
            pair.FirstLoadoutIndex == ShieldedKalisMovementIndex);
        Assert.Contains(cells, pair =>
            pair.IsMirror &&
            pair.FirstLoadoutIndex == ShieldedItakMovementIndex);
        Assert.Single(
            cells,
            pair =>
                pair.FirstLoadoutIndex == ShieldedKalisMovementIndex &&
                pair.SecondLoadoutIndex == ShieldedItakMovementIndex);

        // Canonical ordering: every cell is normalised i <= j, and the slice
        // is a subsequence of the full canonical enumeration in order.
        Assert.All(cells, pair => Assert.True(
            pair.FirstLoadoutIndex <= pair.SecondLoadoutIndex,
            $"Cell ({pair.FirstLoadoutIndex},{pair.SecondLoadoutIndex}) is " +
            "not normalised to canonical i <= j order."));
        Assert.Equal(
            all.Where(pair => pair.RequiresPrecolonialPhilippinesV2).ToList(),
            cells);
    }

    public static TheoryData<int, int> ShieldOneVersusOneCells()
    {
        var data = new TheoryData<int, int>();
        foreach (var pair in MovementScenarioMatrix.EnumerateOneVersusOnePairs())
        {
            if (pair.RequiresPrecolonialPhilippinesV2)
            {
                data.Add(pair.FirstLoadoutIndex, pair.SecondLoadoutIndex);
            }
        }

        return data;
    }

    /// <summary>
    /// Every shield-containing 1v1 cell runs a mirrored duel on each of the
    /// approved seeds <c>1, 2, 3, 5, 8</c>, twice per seed from an identical
    /// construction, and the two runs agree on the state hash, the ordered
    /// event stream, and the outcome. The per-tick movement contract holds
    /// throughout: no agent exceeds the per-axis speed baseline, no agent's
    /// Euclidean step exceeds the baseline by more than the documented
    /// one-raw-unit integer-lattice truncation headroom, every footwork phase
    /// and posture is a declared member, and no lifecycle timer goes negative.
    /// The isolation invariant is the twin run itself: the repeat construction
    /// is built and advanced after the subject run has finished, so a cell
    /// that leaked state across runs would diverge. The progress invariant is
    /// the no-progress streak — a cell that neither terminates nor moves nor
    /// emits an event for the whole budget is a defect, not a slow test.
    /// </summary>
    [Theory]
    [MemberData(nameof(ShieldOneVersusOneCells))]
    public void EveryShieldOneVersusOneCellReplaysIdenticallyOnEveryApprovedSeed(
        int firstIndex,
        int secondIndex)
    {
        foreach (var seed in ApprovedSeeds)
        {
            var scenario = CreateScenario(seed);
            var cellName = $"{CellName(firstIndex, secondIndex)} seed {seed}";

            AgentState[] Build() =>
            [
                CreateAgent(
                    1,
                    factionId: 0,
                    MapCenterXRaw - MirrorOffsetXRaw,
                    MirrorYRaw,
                    scenario,
                    MovementScenarioMatrix.CanonicalLoadouts[firstIndex]),
                CreateAgent(
                    2,
                    factionId: 1,
                    MapCenterXRaw + MirrorOffsetXRaw,
                    MirrorYRaw,
                    scenario,
                    MovementScenarioMatrix.CanonicalLoadouts[secondIndex]),
            ];

            var run = RunToCompletion(scenario, Build(), OneVersusOneTicks);
            var repeat = RunToCompletion(scenario, Build(), OneVersusOneTicks);

            AssertRunContract(run, repeat, cellName, OneVersusOneTicks);
        }
    }

    // ----- The shielded team-matchup slice: 176 of the 231 cells -----

    /// <summary>
    /// Of the 231 team matchups, exactly 176 contain a shielded loadout. The
    /// ten shield-free two-member compositions over <c>KP, WA, KA, IT</c>
    /// give <c>C(10,2) + 10 = 55</c> shield-free matchups, so
    /// <c>231 - 55 = 176</c>. Counted, deduplicated, checked for the twenty-one
    /// team mirrors that contain a shield, and checked for canonical
    /// enumeration order before any simulation runs.
    /// </summary>
    [Fact]
    public void TheShieldTeamMatchupSliceCountsOneHundredSeventySixUniqueCellsInCanonicalOrder()
    {
        var all = MovementScenarioMatrix.EnumerateTeamMatchups();
        var cells = all
            .Where(matchup => matchup.RequiresPrecolonialPhilippinesV2)
            .ToList();

        Assert.Equal(ShieldTeamMatchupCellCount, cells.Count);
        Assert.Equal(ShieldTeamMatchupCellCount, cells.Distinct().Count());

        // Eleven of the twenty-one team compositions contain a shield, and a
        // matchup of one against itself is a mirror, so eleven of the
        // twenty-one team mirrors fall inside the slice.
        var shieldedCompositions = MovementScenarioMatrix
            .EnumerateTeamCompositions()
            .Where(team => team.ContainsShieldedLoadout)
            .ToList();
        Assert.Equal(ShieldOneVersusOneCellCount, shieldedCompositions.Count);
        var mirrors = cells.Where(matchup => matchup.IsMirror).ToList();
        Assert.Equal(ShieldOneVersusOneCellCount, mirrors.Count);
        Assert.Equal(
            shieldedCompositions,
            mirrors.Select(matchup => matchup.FirstTeam).ToList());

        // Canonical ordering: each team is normalised i <= j, and the slice
        // is a subsequence of the full canonical enumeration in order.
        Assert.All(cells, matchup =>
        {
            Assert.True(
                matchup.FirstTeam.FirstMemberIndex <=
                matchup.FirstTeam.SecondMemberIndex,
                "The first team is not normalised to canonical order.");
            Assert.True(
                matchup.SecondTeam.FirstMemberIndex <=
                matchup.SecondTeam.SecondMemberIndex,
                "The second team is not normalised to canonical order.");
        });
        Assert.Equal(
            all.Where(m => m.RequiresPrecolonialPhilippinesV2).ToList(),
            cells);
    }

    public static TheoryData<int, int, int, int> ShieldTeamMatchupCells()
    {
        var data = new TheoryData<int, int, int, int>();
        foreach (var matchup in MovementScenarioMatrix.EnumerateTeamMatchups())
        {
            if (matchup.RequiresPrecolonialPhilippinesV2)
            {
                data.Add(
                    matchup.FirstTeam.FirstMemberIndex,
                    matchup.FirstTeam.SecondMemberIndex,
                    matchup.SecondTeam.FirstMemberIndex,
                    matchup.SecondTeam.SecondMemberIndex);
            }
        }

        return data;
    }

    /// <summary>
    /// Every shield-containing team matchup runs a mirrored 2v2 twice from an
    /// identical construction and holds the same per-tick movement contract,
    /// determinism, and progress invariants as the 1v1 slice.
    /// </summary>
    /// <remarks>
    /// This slice runs at seed one only, deliberately. The full approved seed
    /// set across 176 cells at two runs per cell would put 1,760 simulations
    /// inside one file inside the canonical gate, which the gate does not need
    /// and should not pay for: the seed sweep is exercised end to end by the
    /// eleven 1v1 cells above, which do run all five seeds, and the property
    /// this slice adds is combinatorial coverage of team composition rather
    /// than seed coverage.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ShieldTeamMatchupCells))]
    public void EveryShieldTeamMatchupCellReplaysIdenticallyAtSeedOne(
        int firstTeamFirstIndex,
        int firstTeamSecondIndex,
        int secondTeamFirstIndex,
        int secondTeamSecondIndex)
    {
        var scenario = CreateScenario();
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        var cellName =
            $"{CellName(firstTeamFirstIndex, firstTeamSecondIndex)} vs " +
            CellName(secondTeamFirstIndex, secondTeamSecondIndex);

        AgentState[] Build() =>
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

        var run = RunToCompletion(scenario, Build(), TeamMatchupTicks);
        var repeat = RunToCompletion(scenario, Build(), TeamMatchupTicks);

        AssertRunContract(run, repeat, cellName, TeamMatchupTicks);
    }

    // ----- Helpers -----

    private static string CellName(int firstIndex, int secondIndex) =>
        MovementScenarioMatrix.CanonicalLoadoutCodes[firstIndex] + "-" +
        MovementScenarioMatrix.CanonicalLoadoutCodes[secondIndex];

    private static long SquaredDistance(AgentState first, AgentState second)
    {
        var deltaX = (long)first.XRaw - second.XRaw;
        var deltaY = (long)first.YRaw - second.YRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// Every scenario names its combat preset and its movement preset
    /// explicitly. <c>PrecolonialPhilippinesV2</c> is the only combat preset
    /// that fields both shielded loadouts, so it spans the whole shield
    /// slice, and <c>EquipmentRelativeFootworkV6</c> is the only movement
    /// preset that reads the shield rows. Neither shipped default is touched.
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
    /// shielded matchup is not artificially reach-equal.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout,
        int? attackCooldownTicksOverride = null)
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
            attackCooldownTicksOverride ?? weapon.AttackCooldownTicks,
            loadout);
    }

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
    /// Advances a battle and records the non-authoritative evidence this file
    /// asserts on, without altering a single authoritative field. Copied from
    /// <c>KampilanMovementTests.RunToCompletion</c> — the two merged weapon
    /// sessions duplicated their helpers per file rather than adding a shared
    /// fixture, and a shared fixture would touch a file no weapon session
    /// owns — and extended with the event hash, the no-progress streak, and
    /// the per-agent phase-streak observations the shield cases need.
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
        var maximumDisplacementSquared = (Int128)0;

        var disengageStreak = new Dictionary<ulong, int>();
        var refuseStreak = new Dictionary<ulong, int>();
        var recoverStreak = new Dictionary<ulong, int>();
        var disengageEntries = new Dictionary<ulong, int>();
        var priorPhase = new Dictionary<ulong, FootworkPhase>();
        var maximumDisengageStreak = 0;
        var maximumRefuseStreak = 0;
        var maximumRecoverStreak = 0;
        var maximumDisengageEntries = 0;
        foreach (var agent in agents)
        {
            disengageStreak[agent.EntityId] = 0;
            refuseStreak[agent.EntityId] = 0;
            recoverStreak[agent.EntityId] = 0;
            disengageEntries[agent.EntityId] = 0;
            priorPhase[agent.EntityId] = agent.FootworkPhase;
        }

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
                    if (movedSquared > maximumDisplacementSquared)
                    {
                        maximumDisplacementSquared = movedSquared;
                    }

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

                maximumDisengageStreak = TrackStreak(
                    disengageStreak,
                    agent,
                    FootworkPhase.Disengage,
                    maximumDisengageStreak);
                maximumRefuseStreak = TrackStreak(
                    refuseStreak,
                    agent,
                    FootworkPhase.Refuse,
                    maximumRefuseStreak);
                maximumRecoverStreak = TrackStreak(
                    recoverStreak,
                    agent,
                    FootworkPhase.Recover,
                    maximumRecoverStreak);

                if (agent.FootworkPhase == FootworkPhase.Disengage &&
                    priorPhase[agent.EntityId] != FootworkPhase.Disengage)
                {
                    var entries = disengageEntries[agent.EntityId] + 1;
                    disengageEntries[agent.EntityId] = entries;
                    if (entries > maximumDisengageEntries)
                    {
                        maximumDisengageEntries = entries;
                    }
                }

                priorPhase[agent.EntityId] = agent.FootworkPhase;
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
            maximumNoProgressStreak,
            maximumDisplacementSquared,
            maximumDisengageStreak,
            maximumRefuseStreak,
            maximumRecoverStreak,
            maximumDisengageEntries,
            simulation.MovementConflictDenials);
    }

    private static int TrackStreak(
        Dictionary<ulong, int> streaks,
        AgentState agent,
        FootworkPhase phase,
        int maximum)
    {
        if (agent.FootworkPhase == phase)
        {
            var streak = streaks[agent.EntityId] + 1;
            streaks[agent.EntityId] = streak;
            return streak > maximum ? streak : maximum;
        }

        streaks[agent.EntityId] = 0;
        return maximum;
    }

    private sealed record RunEvidence(
        ulong StateHash,
        List<string> EventStream,
        ulong EventHash,
        BattleOutcome Outcome,
        bool LegalSteps,
        bool LegalPhases,
        string? StepFailure,
        string? PhaseFailure,
        int MaximumNoProgressStreak,
        Int128 MaximumDisplacementSquared,
        int MaximumDisengageStreak,
        int MaximumRefuseStreak,
        int MaximumRecoverStreak,
        int MaximumDisengageEntries,
        long ConflictDenials);
}
