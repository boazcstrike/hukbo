using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The Itak slice of the movement scenario matrix, run as whole battles.
/// The matrix itself (<see cref="MovementScenarioMatrix"/>) only enumerates;
/// this class consumes its cells: the eleven Itak-containing 1v1 pairs run
/// as mirrored duels under a twin-rerun state-hash check, the 176
/// Itak-containing team matchups run as mirrored 2v2 battles under a
/// same-input construction-hash check, and four focused geometries pin the
/// cooperative behaviours of the Itak rows — separate-lane cooperation,
/// ally-blocked refusal, distracted-target entry, and post-ally-death
/// reassessment — on both the solo (<c>IT</c>) and shielded (<c>IS</c>)
/// rows. Every threshold, pace, and distance consumed here is a provisional
/// reconstruction — gameplay tuning, not a historical measurement
/// (docs/research/movement/itak.md, section 7). Every simulation names
/// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> and
/// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> explicitly,
/// and no assertion in this class reads a winner: matchup cells assert the
/// movement contract, never the outcome.
/// </summary>
public sealed class ItakMovementScenarioTests
{
    private const int ItakSoloIndex = 3;

    private const int ItakShieldedIndex = 5;

    private const int OneVersusOneTickBound = 200;

    private const int TeamMatchupTickBound = 120;

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

    private static readonly CombatLoadout SoloItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout ShieldedItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    // ----- The Itak 1v1 slice: eleven cells (design section 17) -----

    /// <summary>
    /// Of the 21 unordered 1v1 pairs, exactly eleven contain an Itak
    /// loadout: the six distinct pairings of <c>IT</c> with the other five
    /// loadouts, the four remaining pairings of <c>IS</c>, and the two
    /// mirrors <c>IT-IT</c> and <c>IS-IS</c>. <c>IT-IS</c> is a single
    /// unordered cell, which is why the count is eleven rather than twelve.
    /// </summary>
    [Fact]
    public void TheItakOneVersusOneSliceCountsElevenDistinctCellsWithBothMirrors()
    {
        var cells = MovementScenarioMatrix
            .EnumerateOneVersusOnePairs()
            .Where(pair => ContainsItak(
                pair.FirstLoadoutIndex, pair.SecondLoadoutIndex))
            .ToList();

        Assert.Equal(11, cells.Count);
        Assert.Equal(11, cells.Distinct().Count());
        Assert.Contains(cells, pair =>
            pair.IsMirror && pair.FirstLoadoutIndex == ItakSoloIndex);
        Assert.Contains(cells, pair =>
            pair.IsMirror && pair.FirstLoadoutIndex == ItakShieldedIndex);
    }

    public static TheoryData<int, int> ItakOneVersusOneCells()
    {
        var data = new TheoryData<int, int>();
        foreach (var pair in MovementScenarioMatrix.EnumerateOneVersusOnePairs())
        {
            if (ContainsItak(pair.FirstLoadoutIndex, pair.SecondLoadoutIndex))
            {
                data.Add(pair.FirstLoadoutIndex, pair.SecondLoadoutIndex);
            }
        }

        return data;
    }

    /// <summary>
    /// Every Itak-containing 1v1 cell runs 200 ticks of mirrored duel —
    /// same row, symmetric x about the map centre — holding the movement
    /// contract on every tick (no agent displaces farther than
    /// <c>MovementSpeedRaw</c> in one tick, and every footwork phase is a
    /// declared member), and a twin rerun of the same construction reaches
    /// a bit-identical state hash.
    /// </summary>
    [Theory]
    [MemberData(nameof(ItakOneVersusOneCells))]
    public void EveryItakOneVersusOneCellHoldsTheMovementContractAndReplaysIdentically(
        int firstIndex,
        int secondIndex)
    {
        var cellName = CellName(firstIndex, secondIndex);
        var subject = CreateOneVersusOne(firstIndex, secondIndex, out var agents);
        var control = CreateOneVersusOne(firstIndex, secondIndex, out _);
        Assert.Equal(control.ComputeStateHash(), subject.ComputeStateHash());

        RunAssertingMovementContract(
            subject, agents, OneVersusOneTickBound, cellName);
        for (var tick = 0; tick < OneVersusOneTickBound; tick++)
        {
            control.AdvanceOneTick();
        }

        Assert.True(
            control.ComputeStateHash() == subject.ComputeStateHash(),
            $"Cell {cellName}: the twin rerun diverged from the subject run.");
    }

    // ----- The Itak team-matchup slice: 176 cells -----

    /// <summary>
    /// Of the 231 team matchups, exactly 176 contain an Itak loadout: the
    /// ten Itak-free compositions over <c>KP, WA, KA, KS</c> give
    /// C(10, 2) + 10 = 55 Itak-free matchups, and 231 - 55 = 176. The three
    /// all-Itak team mirrors are all present.
    /// </summary>
    [Fact]
    public void TheItakTeamMatchupSliceCountsOneHundredSeventySixDistinctCells()
    {
        var cells = MovementScenarioMatrix
            .EnumerateTeamMatchups()
            .Where(matchup =>
                TeamContainsItak(matchup.FirstTeam) ||
                TeamContainsItak(matchup.SecondTeam))
            .ToList();

        Assert.Equal(176, cells.Count);
        Assert.Equal(176, cells.Distinct().Count());
        Assert.Contains(cells, matchup =>
            matchup.IsMirror &&
            matchup.FirstTeam.FirstMemberIndex == ItakSoloIndex &&
            matchup.FirstTeam.SecondMemberIndex == ItakSoloIndex);
        Assert.Contains(cells, matchup =>
            matchup.IsMirror &&
            matchup.FirstTeam.FirstMemberIndex == ItakSoloIndex &&
            matchup.FirstTeam.SecondMemberIndex == ItakShieldedIndex);
        Assert.Contains(cells, matchup =>
            matchup.IsMirror &&
            matchup.FirstTeam.FirstMemberIndex == ItakShieldedIndex &&
            matchup.FirstTeam.SecondMemberIndex == ItakShieldedIndex);
    }

    public static TheoryData<int, int, int, int> ItakTeamMatchupCells()
    {
        var data = new TheoryData<int, int, int, int>();
        foreach (var matchup in MovementScenarioMatrix.EnumerateTeamMatchups())
        {
            if (TeamContainsItak(matchup.FirstTeam) ||
                TeamContainsItak(matchup.SecondTeam))
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
    /// Every Itak-containing team matchup runs 120 ticks of mirrored 2v2 —
    /// team members on shared rows, symmetric x about the map centre —
    /// holding the same per-tick movement contract as the 1v1 slice. The
    /// determinism check here is the same-input construction variant, not a
    /// twin rerun: two simulations built from identical inputs must agree
    /// on the initial state hash, and only one is then advanced. Rerunning
    /// all 176 cells twice would double the slice's wall-clock for a
    /// property the eleven 1v1 twin reruns already exercise end to end.
    /// </summary>
    [Theory]
    [MemberData(nameof(ItakTeamMatchupCells))]
    public void EveryItakTeamMatchupCellHoldsTheMovementContract(
        int firstTeamFirstIndex,
        int firstTeamSecondIndex,
        int secondTeamFirstIndex,
        int secondTeamSecondIndex)
    {
        var cellName =
            $"{CellName(firstTeamFirstIndex, firstTeamSecondIndex)} vs " +
            CellName(secondTeamFirstIndex, secondTeamSecondIndex);
        var subject = CreateTeamMatchup(
            firstTeamFirstIndex,
            firstTeamSecondIndex,
            secondTeamFirstIndex,
            secondTeamSecondIndex,
            out var agents);
        var control = CreateTeamMatchup(
            firstTeamFirstIndex,
            firstTeamSecondIndex,
            secondTeamFirstIndex,
            secondTeamSecondIndex,
            out _);

        Assert.True(
            control.ComputeStateHash() == subject.ComputeStateHash(),
            $"Cell {cellName}: same-input construction produced " +
            "different state hashes.");
        RunAssertingMovementContract(
            subject, agents, TeamMatchupTickBound, cellName);
    }

    // ----- Focused case (a): separate-lane cooperation -----

    /// <summary>
    /// Two same-faction Itak warriors on parallel lanes 1,536 raw apart —
    /// at or beyond both rows' ally clearance radius (solo 1,177 =
    /// 1024 &#215; 11500 / 10000, shielded 1,382 = 1024 &#215; 13500 /
    /// 10000, equality accepts) — close on a mirrored enemy pair without
    /// ever colliding over a lane: neither ally is ever refused or
    /// disengages, both reach the engage band, and on every tick where both
    /// allies' proposals were accepted unchanged their committed positions
    /// sit at or beyond the clearance radius, the friendly-clearance
    /// guarantee observed through whole ticks.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoItakAlliesCooperateOnSeparateLanesTowardTheEnemy(bool shielded)
    {
        var scenario = CreateScenario();
        var loadout = shielded ? ShieldedItak : SoloItak;
        var profile = shielded
            ? TallHardwoodMovementProfiles.ItakRow
            : ItakMovementProfile.Row;
        var firstAlly = CreateAgent(
            1, factionId: 0, MapCenterXRaw - MirrorOffsetXRaw, 50_432,
            scenario, loadout);
        var secondAlly = CreateAgent(
            2, factionId: 0, MapCenterXRaw - MirrorOffsetXRaw, 51_968,
            scenario, loadout);
        var firstEnemy = CreateAgent(
            3, factionId: 1, MapCenterXRaw + MirrorOffsetXRaw, 50_432,
            scenario, loadout);
        var secondEnemy = CreateAgent(
            4, factionId: 1, MapCenterXRaw + MirrorOffsetXRaw, 51_968,
            scenario, loadout);
        var all = new[] { firstAlly, secondAlly, firstEnemy, secondEnemy };
        var simulation = BattleSimulation.CreateForTesting(scenario, all);

        var clearanceRaw = MovementRouteRules.ClearanceRadiusRaw(
            scenario.BodyRadiusRaw,
            profile.AllyClearanceBodyDiametersBasisPoints);
        var clearanceSquared = checked(clearanceRaw * clearanceRaw);
        var initialSquared = SquaredDistance(firstAlly, firstEnemy);
        var firstSawEngage = false;
        var secondSawEngage = false;

        for (var tick = 0; tick < 60; tick++)
        {
            foreach (var agent in all)
            {
                agent.AttackCooldownRemaining = 100;
            }

            simulation.AdvanceOneTick();

            Assert.NotEqual(FootworkPhase.Refuse, firstAlly.FootworkPhase);
            Assert.NotEqual(FootworkPhase.Refuse, secondAlly.FootworkPhase);
            Assert.NotEqual(FootworkPhase.Disengage, firstAlly.FootworkPhase);
            Assert.NotEqual(FootworkPhase.Disengage, secondAlly.FootworkPhase);
            firstSawEngage |= firstAlly.FootworkPhase == FootworkPhase.Engage;
            secondSawEngage |= secondAlly.FootworkPhase == FootworkPhase.Engage;

            if (firstAlly.MovementResolution == MovementResolution.Moved &&
                secondAlly.MovementResolution == MovementResolution.Moved)
            {
                var separationSquared = SquaredDistance(firstAlly, secondAlly);
                Assert.True(
                    separationSquared >= clearanceSquared,
                    $"Tick {simulation.Tick}: both allies moved but their " +
                    $"separation squared {separationSquared} fell inside " +
                    $"the clearance squared {clearanceSquared}.");
            }
        }

        Assert.True(firstSawEngage, "The first ally never reached Engage.");
        Assert.True(secondSawEngage, "The second ally never reached Engage.");
        Assert.True(
            SquaredDistance(firstAlly, firstEnemy) < initialSquared,
            "The first ally never closed on the enemy line.");
        Assert.True(
            SquaredDistance(secondAlly, secondEnemy) < initialSquared,
            "The second ally never closed on the enemy line.");
    }

    // ----- Focused case (b): ally-blocked refusal -----

    /// <summary>
    /// Every candidate lane blocked finalises Refuse from Approach on both
    /// Itak rows. An ally stands 1,100 raw ahead on the direct line to the
    /// enemy. Solo row: the first-tick pace is the 358 acceleration step
    /// (512 &#215; 7000 / 10000), so the direct endpoint sits 742 raw from
    /// the ally and both 22.5-degree obliques about 782 raw — all strictly
    /// inside the 1,177 solo clearance radius. Shielded row: first-tick
    /// pace 332 (512 &#215; 6500 / 10000), direct endpoint 768 raw, both
    /// obliques about 803 raw — all strictly inside the 1,382 shielded
    /// clearance radius. With no surviving candidate the approacher
    /// refuses, holds position, and retains zero pace.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnItakApproachWithEveryLaneAllyBlockedFinalisesRefuse(bool shielded)
    {
        var scenario = CreateScenario();
        var loadout = shielded ? ShieldedItak : SoloItak;
        var actor = CreateAgent(
            1, factionId: 0, 51_200, 51_200, scenario, loadout);
        var allyAhead = CreateAgent(
            2, factionId: 0, 52_300, 51_200, scenario, loadout);
        var enemy = CreateAgent(
            3, factionId: 1, 71_680, 51_200, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, allyAhead, enemy);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Refuse, actor.FootworkPhase);
        Assert.Equal(0, actor.FootworkTicksRemaining);
        Assert.Equal(51_200, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(0, actor.MovementPaceRaw);
    }

    // ----- Focused case (c): distracted-target entry -----

    /// <summary>
    /// A target already engaged by another enemy does not stall the Itak's
    /// entry. The nearest enemy opens in body contact with a fellow warrior
    /// of the Itak's faction — its own target selection picks that warrior,
    /// not the Itak — and a second enemy keeps the global headcounts level
    /// so no posture branch withdraws either side. The Itak, its attack
    /// cooldown pinned so the commit lifecycle never masks the approach,
    /// still closes: it reaches the engage band against the distracted
    /// target within the bound and ends far closer than it began.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnItakStillClosesOnATargetEngagedWithAnotherEnemy(bool shielded)
    {
        var scenario = CreateScenario();
        var loadout = shielded ? ShieldedItak : SoloItak;
        var fellow = CreateAgent(
            1, factionId: 0, 61_440, 50_176, scenario, loadout);
        var actor = CreateAgent(
            2, factionId: 0, 40_960, 51_200, scenario, loadout);
        var target = CreateAgent(
            3, factionId: 1, 61_440, 51_200, scenario, loadout);
        var secondEnemy = CreateAgent(
            4, factionId: 1, 61_440, 49_152, scenario, loadout);
        var all = new[] { fellow, actor, target, secondEnemy };
        var simulation = BattleSimulation.CreateForTesting(scenario, all);

        var initialSquared = SquaredDistance(actor, target);
        var minimumSquared = initialSquared;
        var sawEngage = false;

        for (var tick = 0; tick < 80; tick++)
        {
            foreach (var agent in all)
            {
                agent.AttackCooldownRemaining = 100;
            }

            simulation.AdvanceOneTick();

            if (tick == 0)
            {
                // The distraction under observation: the target's own
                // selection picked the body-contact fellow, not the actor.
                Assert.Equal(fellow.EntityId, target.TargetEntityId);
                Assert.Equal(target.EntityId, actor.TargetEntityId);
            }

            var currentSquared = SquaredDistance(actor, target);
            minimumSquared = Math.Min(minimumSquared, currentSquared);
            sawEngage |= actor.FootworkPhase == FootworkPhase.Engage;
        }

        Assert.True(sawEngage, "The actor never reached Engage.");
        Assert.True(
            minimumSquared < initialSquared,
            "The actor never closed on the distracted target.");

        // The actor entered the band around its distracted target: within
        // the offset-adjusted preferred distance plus one body diameter of
        // slack for the target's own band maintenance.
        var preferredRaw = MovementRouteRules.EffectivePreferredDistanceRaw(
            scenario.AttackRangeRaw,
            shielded
                ? TallHardwoodMovementProfiles.ItakRow
                : ItakMovementProfile.Row,
            MovementRouteRules.CanonicalOpponentIndex(loadout));
        var entryRaw = checked(preferredRaw + (2L * scenario.BodyRadiusRaw));
        Assert.True(
            minimumSquared <= checked(entryRaw * entryRaw),
            $"The actor's closest approach squared {minimumSquared} never " +
            $"entered the band bound squared {checked(entryRaw * entryRaw)}.");
    }

    // ----- Focused case (d): post-ally-death reassessment -----

    /// <summary>
    /// Losing its support flips the ratio, and the flip lands only after
    /// the deaths. Four warriors a side face off at the engage band; the
    /// observed Itak's support ratio is four enemies against four allies —
    /// 10,000 basis points, below both the solo 12,500 and shielded 15,000
    /// disengage entries — so no pre-death tick disengages. When its three
    /// allies die, the very next tick reassesses: one against four is both
    /// past the ratio entry (40,000 basis points) and an unconditional
    /// Withdraw posture, and the Itak disengages. The actor's cooldown is
    /// pinned throughout so no commit lifecycle can defer the transition.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnItakReassessesIntoDisengageOnlyAfterItsAlliesDie(bool shielded)
    {
        var scenario = CreateScenario();
        var loadout = shielded ? ShieldedItak : SoloItak;
        var actor = CreateAgent(
            1, factionId: 0, 51_200, 51_200, scenario, loadout);
        var allies = new[]
        {
            CreateAgent(2, factionId: 0, 51_200, 49_664, scenario, loadout),
            CreateAgent(3, factionId: 0, 51_200, 52_736, scenario, loadout),
            CreateAgent(4, factionId: 0, 51_200, 48_128, scenario, loadout),
        };
        var enemies = new[]
        {
            CreateAgent(5, factionId: 1, 57_344, 51_200, scenario, loadout),
            CreateAgent(6, factionId: 1, 57_344, 49_664, scenario, loadout),
            CreateAgent(7, factionId: 1, 57_344, 52_736, scenario, loadout),
            CreateAgent(8, factionId: 1, 57_344, 48_128, scenario, loadout),
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario, [actor, .. allies, .. enemies]);

        for (var tick = 0; tick < 8; tick++)
        {
            actor.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();
            Assert.NotEqual(FootworkPhase.Disengage, actor.FootworkPhase);
        }

        foreach (var ally in allies)
        {
            ally.HitPoints = 0;
        }

        actor.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Disengage, actor.FootworkPhase);
    }

    // ----- Matrix runners -----

    private static BattleSimulation CreateOneVersusOne(
        int firstIndex,
        int secondIndex,
        out AgentState[] agents)
    {
        var scenario = CreateScenario();
        var west = CreateAgent(
            1,
            factionId: 0,
            MapCenterXRaw - MirrorOffsetXRaw,
            MirrorYRaw,
            scenario,
            MovementScenarioMatrix.CanonicalLoadouts[firstIndex]);
        var east = CreateAgent(
            2,
            factionId: 1,
            MapCenterXRaw + MirrorOffsetXRaw,
            MirrorYRaw,
            scenario,
            MovementScenarioMatrix.CanonicalLoadouts[secondIndex]);
        agents = [west, east];
        return BattleSimulation.CreateForTesting(scenario, agents);
    }

    private static BattleSimulation CreateTeamMatchup(
        int firstTeamFirstIndex,
        int firstTeamSecondIndex,
        int secondTeamFirstIndex,
        int secondTeamSecondIndex,
        out AgentState[] agents)
    {
        var scenario = CreateScenario();
        var loadouts = MovementScenarioMatrix.CanonicalLoadouts;
        agents =
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
        return BattleSimulation.CreateForTesting(scenario, agents);
    }

    /// <summary>
    /// Advances the simulation through the tick bound asserting the
    /// movement contract on every living agent every tick: no one displaces
    /// farther than the scenario's <c>MovementSpeedRaw</c> plus two raw
    /// units in a single tick (compared on squared values — integer
    /// arithmetic only), and every footwork phase is a declared
    /// <see cref="FootworkPhase"/> member. The two-raw-unit headroom is
    /// integer-lattice rounding: the route arithmetic truncates each axis
    /// independently through the 1024-scale oblique and sector tables, so a
    /// committed step can exceed the pace norm by a hair (observed excess
    /// squared 262,145 and 262,288 against 512&#178; = 262,144) without any
    /// agent genuinely outrunning its speed.
    /// </summary>
    private static void RunAssertingMovementContract(
        BattleSimulation simulation,
        AgentState[] agents,
        int tickBound,
        string cellName)
    {
        var speedRaw = checked((long)agents[0].MovementSpeedRaw + 2);
        var speedSquared = checked(speedRaw * speedRaw);
        var previous = new (int XRaw, int YRaw)[agents.Length];

        for (var tick = 0; tick < tickBound; tick++)
        {
            for (var index = 0; index < agents.Length; index++)
            {
                previous[index] = (agents[index].XRaw, agents[index].YRaw);
            }

            simulation.AdvanceOneTick();

            for (var index = 0; index < agents.Length; index++)
            {
                var agent = agents[index];
                var deltaX = (long)agent.XRaw - previous[index].XRaw;
                var deltaY = (long)agent.YRaw - previous[index].YRaw;
                var displacementSquared = checked(
                    (deltaX * deltaX) + (deltaY * deltaY));
                Assert.True(
                    displacementSquared <= speedSquared,
                    $"Cell {cellName}, tick {simulation.Tick}: entity " +
                    $"{agent.EntityId} displaced {displacementSquared} " +
                    $"(squared), beyond the speed bound {speedSquared}.");
                Assert.True(
                    Enum.IsDefined(agent.FootworkPhase),
                    $"Cell {cellName}, tick {simulation.Tick}: entity " +
                    $"{agent.EntityId} carries the undeclared footwork " +
                    $"phase {(byte)agent.FootworkPhase}.");
            }
        }
    }

    // ----- Helpers -----

    private static bool ContainsItak(int firstIndex, int secondIndex) =>
        firstIndex is ItakSoloIndex or ItakShieldedIndex ||
        secondIndex is ItakSoloIndex or ItakShieldedIndex;

    private static bool TeamContainsItak(
        MovementScenarioMatrix.TeamComposition team) =>
        team.FirstMemberIndex is ItakSoloIndex or ItakShieldedIndex ||
        team.SecondMemberIndex is ItakSoloIndex or ItakShieldedIndex;

    private static string CellName(int firstIndex, int secondIndex) =>
        MovementScenarioMatrix.CanonicalLoadoutCodes[firstIndex] + "-" +
        MovementScenarioMatrix.CanonicalLoadoutCodes[secondIndex];

    private static long SquaredDistance(AgentState first, AgentState second)
    {
        var deltaX = (long)first.XRaw - second.XRaw;
        var deltaY = (long)first.YRaw - second.YRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static Scenario CreateScenario() =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 5_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout) =>
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
            loadout);
}
