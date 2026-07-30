using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Task K2 of <c>docs/plans/movement/kalis.md</c>: every equality rule the
/// Kalis rows own, pinned one raw unit outside, exactly on the boundary, and
/// one raw unit inside. The count and lifecycle rules are called directly on
/// the shared pure resolvers with the Kalis thresholds substituted; the
/// distance rule is called directly on the shared route arithmetic and then
/// observed once through a whole tick, so the pipeline is proved to use the
/// same comparison the unit assertions pin.
/// </summary>
/// <remarks>
/// The shared boundary conventions are the foundation session's, not this
/// plan's: disengagement entry is
/// <c>SupportEnemies * 10000 &gt;= SupportAllies * DisengageEnemyToAlly</c>
/// with equality entering, release is the same shape against the reengage
/// threshold with equality leaving, and a target at squared distance exactly
/// equal to the offset-adjusted preferred distance squared enters
/// <c>Engage</c>. Where this plan's wording and the shared implementation
/// differ, these tests assert the shared implementation.
/// Every threshold asserted here is a <b>Provisional reconstruction:
/// gameplay tuning; no historical measurement</b>.
/// </remarks>
public sealed class KalisMovementTransitionTests
{
    private static readonly CombatLoadout SoloKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout ShieldedKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static LoadoutMovementProfile Solo => KalisMovementProfile.Row;

    private static LoadoutMovementProfile Shielded =>
        TallHardwoodMovementProfiles.KalisRow;

    /// <summary>
    /// The bodies are 512 raw in radius and move 512 raw per tick, so a solo
    /// Kalis wants pace 496 forward (512 * 9700 / 10000), 455 lateral, 389
    /// backward and 168 committed, accelerates 307 and decelerates 358 per
    /// tick; a shielded Kalis wants 481 forward, 430 lateral, 343 backward
    /// and 153 committed, accelerating 286 and decelerating 307. With attack
    /// range 5120 the solo preferred distance against another solo Kalis is
    /// 6144 (5120 * 12000 / 10000) and the shielded one against another
    /// shielded Kalis is 6656.
    /// </summary>
    private const int SoloAccelerationStepRaw = 307;

    private const int SoloCommittedPaceRaw = 168;

    private const int SoloVersusSoloPreferredRaw = 6_144;

    private const int ShieldedVersusShieldedPreferredRaw = 6_656;

    // ----- Count definition and the entry equality (plan rules 1 to 3) -----

    /// <summary>
    /// Solo Kalis enters disengagement when
    /// <c>enemies * 2 &gt;= allies * 3</c>, the integer reading of its
    /// 15,000-basis-point entry threshold. Two allies against three enemies
    /// is exact equality and enters; three against four is one step short
    /// and does not.
    /// </summary>
    [Theory]
    [InlineData(2, 3, true)]
    [InlineData(3, 4, false)]
    [InlineData(1, 2, true)]
    [InlineData(2, 2, false)]
    public void SoloKalisEntersDisengagementOnEntryEquality(
        int allies, int enemies, bool expectedEntry)
    {
        var (phase, ticks) = Resolve(Solo, allies, enemies);

        Assert.Equal(expectedEntry, phase == FootworkPhase.Disengage);
        Assert.Equal(0, ticks);
    }

    /// <summary>
    /// Shielded Kalis enters when <c>enemies * 4 &gt;= allies * 7</c>, the
    /// integer reading of its 17,500-basis-point entry threshold. Four
    /// allies against seven enemies is exact equality and enters; four
    /// against six does not.
    /// </summary>
    [Theory]
    [InlineData(4, 7, true)]
    [InlineData(4, 6, false)]
    [InlineData(2, 4, true)]
    [InlineData(2, 3, false)]
    public void ShieldedKalisEntersDisengagementOnEntryEquality(
        int allies, int enemies, bool expectedEntry)
    {
        var (phase, ticks) = Resolve(Shielded, allies, enemies);

        Assert.Equal(expectedEntry, phase == FootworkPhase.Disengage);
        Assert.Equal(0, ticks);
    }

    /// <summary>
    /// The shielded row tolerates a worse local count than the solo row
    /// before it leaves: at three allies against five enemies the solo row
    /// enters and the shielded row does not. The plan calls this out
    /// explicitly as an intentional, reviewable consequence of the 7:4
    /// threshold rather than an accident.
    /// </summary>
    [Fact]
    public void TheShieldedRowToleratesTheThreeVersusFiveCountTheSoloRowLeaves()
    {
        Assert.Equal(
            FootworkPhase.Disengage,
            Resolve(Solo, allies: 3, enemies: 5).Phase);
        Assert.NotEqual(
            FootworkPhase.Disengage,
            Resolve(Shielded, allies: 3, enemies: 5).Phase);
    }

    // ----- The release equality (plan rule 4) -----

    /// <summary>
    /// Both rows release on <c>enemies * 10 &lt;= allies * 11</c>, the
    /// integer reading of the shared 11,000-basis-point reengage threshold.
    /// Ten allies against eleven enemies is exact equality and leaves; ten
    /// against twelve is strictly between the two thresholds and preserves
    /// the prior disengagement.
    /// </summary>
    [Theory]
    [InlineData(10, 11, false)]
    [InlineData(10, 12, true)]
    public void BothRowsReleaseDisengagementOnReleaseEquality(
        int allies, int enemies, bool expectedRetention)
    {
        foreach (var row in new[] { Solo, Shielded })
        {
            var (phase, _) = Resolve(
                row, allies, enemies, priorPhase: FootworkPhase.Disengage);

            Assert.Equal(
                expectedRetention, phase == FootworkPhase.Disengage);
        }
    }

    /// <summary>
    /// Hysteresis exists on both rows: a count strictly between the entry
    /// and release thresholds preserves whichever state the warrior already
    /// held. Solo Kalis at four allies against five enemies is above 11:10
    /// and below 3:2; shielded Kalis at the same count is above 11:10 and
    /// below 7:4.
    /// </summary>
    [Fact]
    public void ACountBetweenTheThresholdsPreservesThePriorState()
    {
        foreach (var row in new[] { Solo, Shielded })
        {
            Assert.Equal(
                FootworkPhase.Disengage,
                Resolve(
                    row,
                    allies: 4,
                    enemies: 5,
                    priorPhase: FootworkPhase.Disengage).Phase);
            Assert.NotEqual(
                FootworkPhase.Disengage,
                Resolve(row, allies: 4, enemies: 5).Phase);
        }
    }

    // ----- The zero-enemy rule and self-inclusion (plan rules 1, 2) -----

    /// <summary>
    /// Plan rule 2: with no living perceived enemy neither row can enter or
    /// remain in disengagement, whatever the cross-multiplication would say
    /// and whatever the prior phase was.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.None)]
    [InlineData(FootworkPhase.Disengage)]
    public void NeitherRowDisengagesWithNoLivingEnemy(FootworkPhase prior)
    {
        foreach (var row in new[] { Solo, Shielded })
        {
            var (phase, _) = Resolve(
                row, allies: 1, enemies: 0, priorPhase: prior);

            Assert.NotEqual(FootworkPhase.Disengage, phase);
        }
    }

    /// <summary>
    /// Plan rule 1: the ally side counts the actor itself, so a lone
    /// warrior's ratio is well defined. One solo Kalis against two enemies
    /// is 2.0, past its 1.5 entry; one shielded Kalis against two enemies is
    /// 2.0, past its 1.75 entry.
    /// </summary>
    [Fact]
    public void TheActorCountsItselfOnTheAllySide()
    {
        foreach (var row in new[] { Solo, Shielded })
        {
            Assert.Equal(
                FootworkPhase.Disengage,
                Resolve(row, allies: 1, enemies: 2).Phase);
        }
    }

    /// <summary>
    /// A dead Kalis carries no phase and no timer, resolved before every
    /// count rule — dead agents count nowhere, including on their own side.
    /// </summary>
    [Fact]
    public void ADeadKalisCarriesNoPhaseAndNoTimer()
    {
        foreach (var row in new[] { Solo, Shielded })
        {
            Assert.Equal(
                (FootworkPhase.None, 0),
                Resolve(
                    row,
                    allies: 1,
                    enemies: 9,
                    isAlive: false,
                    priorPhase: FootworkPhase.Commit,
                    priorTicksRemaining: 5));
        }
    }

    /// <summary>
    /// Plan rule 12's arithmetic safety: the ratio is a widened checked
    /// integer cross-product with no division anywhere, so counts at the
    /// extreme of <see cref="int"/> resolve without overflowing. Both
    /// directions are exercised: an overwhelming enemy count enters, and an
    /// overwhelming ally count does not.
    /// </summary>
    [Fact]
    public void TheRatioCrossProductSurvivesExtremeCounts()
    {
        foreach (var row in new[] { Solo, Shielded })
        {
            Assert.Equal(
                FootworkPhase.Disengage,
                Resolve(row, allies: 1, enemies: int.MaxValue).Phase);
            Assert.NotEqual(
                FootworkPhase.Disengage,
                Resolve(row, allies: int.MaxValue, enemies: 1).Phase);
            Assert.NotEqual(
                FootworkPhase.Disengage,
                Resolve(row, allies: int.MaxValue, enemies: int.MaxValue)
                    .Phase);
        }
    }

    // ----- The preferred-distance equality (plan rule 5) -----

    /// <summary>
    /// The offset-adjusted preferred distance of every Kalis-versus-opponent
    /// pairing, in raw units against a 5120-raw attack range. The offsets run
    /// in canonical opponent order <c>KP, WA, KA, IT, KS, IS</c>, so a solo
    /// Kalis stands closer to a Kampilan (11,500 basis points) than to a
    /// shielded Itak (12,500), and the flat "1.20 reach" reading of the plan
    /// holds only against another solo Kalis, whose offset cell is zero.
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Theory]
    [InlineData(0, 5_888, 6_528)]
    [InlineData(1, 6_016, 6_656)]
    [InlineData(2, 6_144, 6_784)]
    [InlineData(3, 6_272, 6_912)]
    [InlineData(4, 6_272, 6_656)]
    [InlineData(5, 6_400, 6_784)]
    public void TheEffectivePreferredDistanceIsOffsetPerOpponent(
        int opponentIndex, int expectedSolo, int expectedShielded)
    {
        Assert.Equal(
            expectedSolo,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, Solo, opponentIndex));
        Assert.Equal(
            expectedShielded,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, Shielded, opponentIndex));
    }

    /// <summary>
    /// The canonical opponent index the offsets are read at is the equipment
    /// triple's, and it is rank-independent, so an opponent's social
    /// standing never changes the distance a Kalis warrior keeps from it.
    /// </summary>
    [Fact]
    public void TheOpponentIndexIgnoresRank()
    {
        Assert.Equal(
            MovementRouteRules.CanonicalOpponentIndex(SoloKalis),
            MovementRouteRules.CanonicalOpponentIndex(
                SoloKalis with { Rank = RankId.Datu }));
        Assert.Equal(
            MovementRouteRules.CanonicalOpponentIndex(ShieldedKalis),
            MovementRouteRules.CanonicalOpponentIndex(
                ShieldedKalis with { Rank = RankId.Ayuey }));
    }

    /// <summary>
    /// Entry equality on the distance rule, observed through a whole tick:
    /// two solo Kalis warriors exactly 6144 raw apart both resolve
    /// <c>Engage</c>, one raw unit further apart both resolve
    /// <c>Approach</c>, and one raw unit closer both resolve
    /// <c>Engage</c>. The comparison is inclusive on squared values.
    /// </summary>
    [Theory]
    [InlineData(SoloVersusSoloPreferredRaw - 1, FootworkPhase.Engage)]
    [InlineData(SoloVersusSoloPreferredRaw, FootworkPhase.Engage)]
    [InlineData(SoloVersusSoloPreferredRaw + 1, FootworkPhase.Approach)]
    public void SoloKalisEntersEngageAtTheInclusivePreferredDistance(
        int separationRaw, FootworkPhase expected)
    {
        var (west, east) = RunOneDuelTick(SoloKalis, separationRaw);

        Assert.Equal(expected, west.FootworkPhase);
        Assert.Equal(expected, east.FootworkPhase);
    }

    /// <summary>
    /// The same equality on the shielded row, whose preferred distance
    /// against another shielded Kalis is 6656 raw rather than 6144 — the
    /// shielded warrior enters its engagement band further out.
    /// </summary>
    [Theory]
    [InlineData(ShieldedVersusShieldedPreferredRaw - 1, FootworkPhase.Engage)]
    [InlineData(ShieldedVersusShieldedPreferredRaw, FootworkPhase.Engage)]
    [InlineData(
        ShieldedVersusShieldedPreferredRaw + 1, FootworkPhase.Approach)]
    public void ShieldedKalisEntersEngageAtTheInclusivePreferredDistance(
        int separationRaw, FootworkPhase expected)
    {
        var (west, east) = RunOneDuelTick(ShieldedKalis, separationRaw);

        Assert.Equal(expected, west.FootworkPhase);
        Assert.Equal(expected, east.FootworkPhase);
    }

    /// <summary>
    /// Preferred distance is not a stop line and never changes combat reach:
    /// at exact equality the warriors are in <c>Engage</c> and still outside
    /// the 5120-raw attack range, and they keep closing rather than parking
    /// on the band.
    /// </summary>
    [Fact]
    public void ThePreferredDistanceIsNotAStopLine()
    {
        var (west, east) = RunOneDuelTick(
            SoloKalis, SoloVersusSoloPreferredRaw);

        Assert.Equal(FootworkPhase.Engage, west.FootworkPhase);
        Assert.True(
            east.XRaw - west.XRaw < SoloVersusSoloPreferredRaw,
            "Neither warrior closed, so Engage was treated as a stop line.");
        Assert.True(
            east.XRaw - west.XRaw > AttackRangeRaw,
            "The pair closed past attack range, so this tick proves nothing " +
            "about crossing the band without an attack.");
    }

    // ----- Direction bands and turn caps (plan rules 6 and 7) -----

    /// <summary>
    /// Plan rule 6: circular facing-to-travel separation 0 through 1 is
    /// forward, 2 through 5 is lateral, and 6 through 8 is backward. The
    /// shared classifier makes the choice; these are the caps it returns for
    /// the two Kalis rows.
    /// </summary>
    [Theory]
    [InlineData(0, 9_700, 9_400)]
    [InlineData(1, 9_700, 9_400)]
    [InlineData(2, 8_900, 8_400)]
    [InlineData(5, 8_900, 8_400)]
    [InlineData(6, 7_600, 6_700)]
    [InlineData(8, 7_600, 6_700)]
    public void TheDirectionBandsReturnTheKalisPaceCaps(
        int separation, int expectedSolo, int expectedShielded)
    {
        Assert.Equal(
            expectedSolo,
            FacingRules.DirectionBandPaceCapBasisPoints(Solo, separation));
        Assert.Equal(
            expectedShielded,
            FacingRules.DirectionBandPaceCapBasisPoints(
                Shielded, separation));
    }

    /// <summary>
    /// Plan rule 7: a turn request exactly at the two-sector ordinary cap
    /// reaches the desired facing, and one sector beyond it advances only by
    /// the cap. Both Kalis rows carry the same budget.
    /// </summary>
    [Fact]
    public void AnOrdinaryTurnRequestAtTheCapReachesTheDesiredFacing()
    {
        foreach (var row in new[] { Solo, Shielded })
        {
            Assert.Equal(
                Facing16.SouthEast,
                FacingRules.TurnToward(
                    Facing16.East,
                    Facing16.SouthEast,
                    row.MaximumFacingStepsPerTick,
                    factionId: 0));
            Assert.Equal(
                Facing16.SouthEast,
                FacingRules.TurnToward(
                    Facing16.East,
                    Facing16.SouthSouthEast,
                    row.MaximumFacingStepsPerTick,
                    factionId: 0));
        }
    }

    /// <summary>
    /// The committed budget is one sector on both rows, so a committed Kalis
    /// warrior asked for a two-sector turn advances exactly one.
    /// </summary>
    [Fact]
    public void ACommittedTurnRequestAdvancesOnlyOneSector()
    {
        foreach (var row in new[] { Solo, Shielded })
        {
            Assert.Equal(
                Facing16.EastSouthEast,
                FacingRules.TurnToward(
                    Facing16.East,
                    Facing16.SouthEast,
                    row.CommittedFacingStepsPerTick,
                    factionId: 0));
        }
    }

    /// <summary>
    /// The ordinary cap through a whole tick: a threat due north is four
    /// counter-clockwise sectors from the opening East facing, and the
    /// two-sector Kalis budget reaches NorthEast on the first tick and North
    /// on the second.
    /// </summary>
    [Fact]
    public void TheOrdinaryTurnCapBindsThroughTheWholeTick()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, 0, 51_200, 51_200, scenario, SoloKalis);
        var enemy = CreateAgent(2, 1, 51_200, 30_720, scenario, SoloKalis);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        simulation.AdvanceOneTick();
        Assert.Equal(Facing16.NorthEast, actor.Facing);

        simulation.AdvanceOneTick();
        Assert.Equal(Facing16.North, actor.Facing);
    }

    /// <summary>
    /// The committed cap through a whole tick: the same due-north threat
    /// turns a committed Kalis warrior by exactly one sector.
    /// </summary>
    [Fact]
    public void TheCommittedTurnCapBindsThroughTheWholeTick()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, 0, 51_200, 51_200, scenario, SoloKalis);
        var enemy = CreateAgent(2, 1, 51_200, 30_720, scenario, SoloKalis);
        actor.FootworkPhase = FootworkPhase.Commit;
        actor.FootworkTicksRemaining = 5;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        simulation.AdvanceOneTick();

        Assert.Equal(Facing16.EastNorthEast, actor.Facing);
        Assert.Equal(FootworkPhase.Commit, actor.FootworkPhase);
        Assert.Equal(4, actor.FootworkTicksRemaining);
    }

    // ----- Commitment and recovery (plan rules 8, 9, 10) -----

    /// <summary>
    /// Plan rule 8: an attack accepted after movement on tick <c>T</c> does
    /// not change that tick's movement. The pair opens at exactly attack
    /// range, closes at the ordinary 307-unit acceleration step, and only
    /// then attacks — the committed 168-unit cap binds from <c>T+1</c>, not
    /// retroactively.
    /// </summary>
    [Fact]
    public void AnAcceptedAttackDoesNotCapTheMovementOfItsOwnTick()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, 0, 92_160, 51_200, scenario, SoloKalis);
        var east = CreateAgent(
            2, 1, 92_160 + AttackRangeRaw, 51_200, scenario, SoloKalis);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        simulation.AdvanceOneTick();

        Assert.Equal(SoloAccelerationStepRaw, west.MovementPaceRaw);
        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(Solo.CommitmentTicks, west.FootworkTicksRemaining);

        west.AttackCooldownRemaining = 100;
        east.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();

        Assert.Equal(SoloCommittedPaceRaw, west.MovementPaceRaw);
    }

    /// <summary>
    /// Plan rules 8 and 9 on the solo row: two whole commitment ticks
    /// counted inclusive of the entry tick, then two whole recovery ticks,
    /// then a return to <c>Engage</c> against a target still inside the
    /// preferred distance. Cooldowns are pinned high between ticks so no
    /// second attack re-enters commitment mid-sequence.
    /// </summary>
    [Fact]
    public void SoloKalisCommitsForTwoTicksAndRecoversForTwo()
    {
        AssertCommitLifecycle(
            SoloKalis,
            [
                (FootworkPhase.Commit, 1),
                (FootworkPhase.Recover, 2),
                (FootworkPhase.Recover, 1),
                (FootworkPhase.Engage, 0),
            ]);
    }

    /// <summary>
    /// The same lifecycle on the shielded row, which commits for three ticks
    /// and recovers for three: a shielded Kalis warrior is out of position
    /// for longer after every attack than a solo one.
    /// </summary>
    [Fact]
    public void ShieldedKalisCommitsForThreeTicksAndRecoversForThree()
    {
        AssertCommitLifecycle(
            ShieldedKalis,
            [
                (FootworkPhase.Commit, 2),
                (FootworkPhase.Commit, 1),
                (FootworkPhase.Recover, 3),
                (FootworkPhase.Recover, 2),
                (FootworkPhase.Recover, 1),
                (FootworkPhase.Engage, 0),
            ]);
    }

    /// <summary>
    /// Plan rule 10: an attack accepted through the unchanged combat gates
    /// interrupts recovery and starts a fresh commitment at the profile's
    /// full duration, on both rows.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAcceptedAttackInterruptsRecoveryWithAFreshCommitment(
        bool shielded)
    {
        var loadout = shielded ? ShieldedKalis : SoloKalis;
        var profile = shielded ? Shielded : Solo;
        var scenario = CreateScenario();
        var west = CreateAgent(1, 0, 92_160, 51_200, scenario, loadout);
        var east = CreateAgent(2, 1, 93_184, 51_200, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);
        west.FootworkPhase = FootworkPhase.Recover;
        west.FootworkTicksRemaining = 5;

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(profile.CommitmentTicks, west.FootworkTicksRemaining);
    }

    /// <summary>
    /// Plan rule 12's speed ceiling: no Kalis proposal, in any phase, moves
    /// a warrior further in one tick than its own
    /// <c>MovementSpeedRaw</c>, and a favourable local count never raises
    /// that ceiling. Observed over a crowded run in which one side badly
    /// outnumbers the other.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoKalisProposalEverExceedsTheSpeedCeiling(bool shielded)
    {
        var loadout = shielded ? ShieldedKalis : SoloKalis;
        var scenario = CreateScenario();
        var agents = new List<AgentState>();
        for (var index = 0; index < 6; index++)
        {
            agents.Add(CreateAgent(
                checked((ulong)index + 1),
                factionId: 0,
                92_160,
                45_056 + (index * 2_048),
                scenario,
                loadout));
        }

        agents.Add(CreateAgent(
            7, factionId: 1, 102_400, 51_200, scenario, loadout));
        agents.Add(CreateAgent(
            8, factionId: 1, 102_400, 53_248, scenario, loadout));

        var simulation = BattleSimulation.CreateForTesting(
            scenario, [.. agents]);
        var previous = agents
            .Select(agent => (agent.XRaw, agent.YRaw))
            .ToArray();

        for (var tick = 0; tick < 120; tick++)
        {
            simulation.AdvanceOneTick();

            for (var index = 0; index < agents.Count; index++)
            {
                var agent = agents[index];
                var movedSquared = CollisionGeometry.SquaredDistance(
                    previous[index].XRaw,
                    previous[index].YRaw,
                    agent.XRaw,
                    agent.YRaw);

                Assert.True(
                    movedSquared <=
                        (long)scenario.MovementSpeedRaw *
                        scenario.MovementSpeedRaw,
                    $"Agent {agent.EntityId} moved further than its own " +
                    $"speed on tick {simulation.Tick}.");
                Assert.True(
                    agent.MovementPaceRaw <= scenario.MovementSpeedRaw,
                    $"Agent {agent.EntityId} retained a pace above its own " +
                    $"speed on tick {simulation.Tick}.");
                previous[index] = (agent.XRaw, agent.YRaw);
            }
        }
    }

    // ----- Caller-order independence (plan rule 11) -----

    /// <summary>
    /// Plan rule 11: the same battle handed to the simulation in the reverse
    /// caller order produces the same ordered result, agent for agent. The
    /// decision inputs are counts and squared distances with stable
    /// <c>EntityId</c> tie-breaks, never collection insertion order.
    /// </summary>
    /// <remarks>
    /// <c>CreateForTesting</c> canonicalises its input by <c>EntityId</c>, so
    /// this pins caller-order independence at the construction boundary. The
    /// deeper storage-order independence of the context query itself is
    /// pinned by <c>MovementContextObservationTests</c> against the naive
    /// oracle over permuted spans, which this test deliberately does not
    /// duplicate.
    /// </remarks>
    [Fact]
    public void ReversingTheCallerOrderProducesTheSameOrderedResult()
    {
        var forward = BuildCrowdedRoster();
        var reversed = BuildCrowdedRoster();
        Array.Reverse(reversed);

        var first = BattleSimulation.CreateForTesting(CreateScenario(), forward);
        var second = BattleSimulation.CreateForTesting(
            CreateScenario(), reversed);

        for (var tick = 0; tick < 60; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();

            Assert.Equal(first.ComputeStateHash(), second.ComputeStateHash());
            Assert.Equal(first.Outcome, second.Outcome);
        }

        foreach (var agent in forward)
        {
            var mirror = Array.Find(
                reversed, other => other.EntityId == agent.EntityId);

            Assert.NotNull(mirror);
            Assert.Equal(agent.XRaw, mirror.XRaw);
            Assert.Equal(agent.YRaw, mirror.YRaw);
            Assert.Equal(agent.Facing, mirror.Facing);
            Assert.Equal(agent.FootworkPhase, mirror.FootworkPhase);
            Assert.Equal(
                agent.FootworkTicksRemaining, mirror.FootworkTicksRemaining);
        }
    }

    // ----- Helpers -----

    private const int AttackRangeRaw = 5 * FixedPoint.Scale;

    private static (FootworkPhase Phase, int TicksRemaining) Resolve(
        LoadoutMovementProfile profile,
        int allies,
        int enemies,
        FootworkPhase priorPhase = FootworkPhase.None,
        int priorTicksRemaining = 0,
        bool isAlive = true,
        TacticalPosture posture = TacticalPosture.Hold) =>
        WeaponMovementRules.ResolveProvisionalFootwork(
            isAlive,
            priorPhase,
            priorTicksRemaining,
            posture,
            allies,
            enemies,
            profile.DisengageEnemyToAllyBasisPoints,
            profile.ReengageEnemyToAllyBasisPoints,
            profile.RecoveryTicks,
            hasTarget: false,
            targetAtOrInsidePreferredDistance: false);

    /// <summary>
    /// Runs one tick of a mirrored duel between two warriors of the same
    /// loadout, separated on the x axis by exactly
    /// <paramref name="separationRaw"/> raw units.
    /// </summary>
    private static (AgentState West, AgentState East) RunOneDuelTick(
        CombatLoadout loadout, int separationRaw)
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, 0, 92_160, 51_200, scenario, loadout);
        var east = CreateAgent(
            2, 1, 92_160 + separationRaw, 51_200, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        simulation.AdvanceOneTick();

        return (west, east);
    }

    /// <summary>
    /// Drives a body-contact duel through its whole attack lifecycle and
    /// asserts the expected phase and timer after each tick beyond the
    /// first, with cooldowns pinned high so nothing re-enters commitment.
    /// </summary>
    private static void AssertCommitLifecycle(
        CombatLoadout loadout,
        (FootworkPhase Phase, int Ticks)[] expected)
    {
        var profile = MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .ResolveLoadoutProfile(loadout);
        var scenario = CreateScenario();
        var west = CreateAgent(1, 0, 92_160, 51_200, scenario, loadout);
        var east = CreateAgent(2, 1, 93_184, 51_200, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        simulation.AdvanceOneTick();
        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(profile.CommitmentTicks, west.FootworkTicksRemaining);

        foreach (var (phase, ticks) in expected)
        {
            west.AttackCooldownRemaining = 100;
            east.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();

            Assert.Equal(phase, west.FootworkPhase);
            Assert.Equal(ticks, west.FootworkTicksRemaining);
        }
    }

    private static AgentState[] BuildCrowdedRoster()
    {
        var scenario = CreateScenario();
        return
        [
            CreateAgent(1, 0, 92_160, 47_104, scenario, SoloKalis),
            CreateAgent(2, 0, 92_160, 51_200, scenario, ShieldedKalis),
            CreateAgent(3, 0, 92_160, 55_296, scenario, SoloKalis),
            CreateAgent(4, 1, 102_400, 47_104, scenario, ShieldedKalis),
            CreateAgent(5, 1, 102_400, 51_200, scenario, SoloKalis),
            CreateAgent(6, 1, 102_400, 55_296, scenario, ShieldedKalis),
        ];
    }

    /// <summary>
    /// The shared duel scenario: combat preset
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> named
    /// explicitly, because it is the only preset fielding all six canonical
    /// loadouts and therefore the only one under which a shielded Kalis
    /// warrior exists at all.
    /// </summary>
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
            AttackRangeRaw = AttackRangeRaw,
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
