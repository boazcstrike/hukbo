using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The Itak-specific movement scenarios: four focused geometries pinning the
/// cooperative behaviours of the Itak rows — separate-lane cooperation,
/// ally-blocked refusal, distracted-target entry, and post-ally-death
/// reassessment — on both the solo (<c>IT</c>) and shielded (<c>IS</c>) rows,
/// the curated count boundaries at which each row disengages, roster
/// preservation under the explicit combat V2 preset, legacy-preset neutrality,
/// and an Itak-heavy long-run determinism case. Every threshold, pace, and
/// distance consumed here is a provisional reconstruction — gameplay tuning,
/// not a historical measurement (docs/research/movement/itak.md, section 7).
/// Every simulation names <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>
/// and <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> explicitly,
/// and no assertion in this class reads a winner.
/// </summary>
/// <remarks>
/// The Itak slice of the movement scenario matrix used to live here — the
/// eleven Itak-containing 1v1 pairs and the 176 Itak-containing team
/// matchups, under a twin-rerun and a same-input construction-hash check. The
/// Kalis and tall-hardwood-shield suites each held an overlapping slice of the
/// same matrix under a contract of their own, so most cells ran two or three
/// times and each run applied only part of the union. Every cell of the matrix
/// now runs exactly once, under the union of all three contracts, in
/// <see cref="MovementMatrixContractTests"/>. What remains in this file is
/// what is genuinely about the Itak rows rather than about the matrix.
/// </remarks>
public sealed class ItakMovementScenarioTests
{
    /// <summary>Half of the 200-cell-wide map, in raw units.</summary>
    private const int MapCenterXRaw = 102_400;

    /// <summary>The mirrored lane offset from the map centre.</summary>
    private const int MirrorOffsetXRaw = 10_240;

    private static readonly CombatLoadout SoloItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout ShieldedItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

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

    // ----- Shielded rosters preserved under explicit combat V2 -----

    /// <summary>
    /// The shipped default combat preset is the solo-only, four-entry
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV6"/> roster, so a
    /// six-entry Itak roster survives only by naming
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> explicitly:
    /// the same counts validate under the explicit V2 preset and are a
    /// roster-length mismatch under the default.
    /// </summary>
    [Fact]
    public void TheSixEntryItakRosterValidatesOnlyUnderTheExplicitCombatVTwoPreset()
    {
        var preserved = Scenario.CreateDefault(seed: 1, totalAgents: 8) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = ImmutableArray.Create(0, 0, 0, 0, 0, 4),
        };

        preserved.Validate();

        var defaulted = Scenario.CreateDefault(seed: 1, totalAgents: 8) with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = ImmutableArray.Create(0, 0, 0, 0, 0, 4),
        };

        Assert.Equal(
            CombatPresetId.PrecolonialPhilippinesV6, defaulted.CombatPreset);
        Assert.Throws<ArgumentException>(defaulted.Validate);
    }

    /// <summary>
    /// The V2 roster is indexed weapon-first, solo before paired: index four
    /// is the solo Itak entry and index five the shielded one. A scenario
    /// naming the explicit V2 preset with all of one faction's warriors on a
    /// single Itak roster entry spawns that exact loadout on every warrior,
    /// and each spawned loadout resolves to the pinned profile row instance
    /// under <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnExplicitVTwoItakRosterSpawnsTheNamedLoadoutOnEveryWarrior(
        bool shielded)
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 8) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = shielded
                ? ImmutableArray.Create(0, 0, 0, 0, 0, 4)
                : ImmutableArray.Create(0, 0, 0, 0, 4, 0),
        };
        var ruleset = MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV6);
        var expectedShield = shielded ? ShieldId.TallHardwood : ShieldId.None;
        var expectedRow = shielded
            ? TallHardwoodMovementProfiles.ItakRow
            : ItakMovementProfile.Row;

        var simulation = BattleSimulation.Create(scenario);

        Assert.Equal(8, simulation.Agents.Count);
        Assert.All(simulation.Agents, agent =>
        {
            Assert.Equal(WeaponId.Itak, agent.Loadout.Weapon);
            Assert.Equal(ArmorId.LightOrganic, agent.Loadout.Armor);
            Assert.Equal(expectedShield, agent.Loadout.Shield);
            Assert.Same(
                expectedRow, ruleset.ResolveLoadoutProfile(agent.Loadout));
        });
    }

    /// <summary>
    /// End-to-end event-stream shape under an explicit V2 Itak roster, the
    /// same five fields the headless feed consumes: every event carries a
    /// declared kind, the emitting tick, a source that spawned in this
    /// battle (the outcome event's zero sentinel excepted), a target that is
    /// absent or spawned, and a non-negative value. Every attack in an
    /// all-Itak battle carries <see cref="WeaponId.Itak"/> and the roster
    /// entry's shield, which is what preserving the shielded row end to end
    /// means: the shield chosen at the roster index is the one the event
    /// stream reports on every blow.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnExplicitVTwoItakBattleStreamsWellFormedEventsEndToEnd(
        bool shielded)
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 12) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = shielded
                ? ImmutableArray.Create(0, 0, 0, 0, 0, 6)
                : ImmutableArray.Create(0, 0, 0, 0, 6, 0),
        };
        var expectedShield = shielded ? ShieldId.TallHardwood : ShieldId.None;
        var simulation = BattleSimulation.Create(scenario);
        var knownIds = new HashSet<ulong>();
        foreach (var agent in simulation.Agents)
        {
            knownIds.Add(agent.EntityId);
        }

        var sawAttack = false;
        for (var tick = 0;
            tick < 2_000 && simulation.Outcome == BattleOutcome.Ongoing;
            tick++)
        {
            simulation.AdvanceOneTick();
            foreach (var battleEvent in simulation.LastEvents)
            {
                Assert.True(
                    Enum.IsDefined(battleEvent.Kind),
                    $"Tick {simulation.Tick}: undeclared event kind " +
                    $"{(int)battleEvent.Kind}.");
                Assert.Equal(simulation.Tick, battleEvent.Tick);
                if (battleEvent.Kind == BattleEventKind.Outcome)
                {
                    Assert.Equal(0UL, battleEvent.SourceEntityId);
                }
                else
                {
                    Assert.Contains(battleEvent.SourceEntityId, knownIds);
                }

                if (battleEvent.TargetEntityId is { } targetId)
                {
                    Assert.Contains(targetId, knownIds);
                }

                Assert.True(
                    battleEvent.Value >= 0,
                    $"Tick {simulation.Tick}: event value " +
                    $"{battleEvent.Value} is negative.");
                if (battleEvent.Kind == BattleEventKind.Attack)
                {
                    sawAttack = true;
                    Assert.Equal(WeaponId.Itak, battleEvent.Weapon);
                    Assert.Equal(expectedShield, battleEvent.Shield);
                    Assert.NotNull(battleEvent.TargetEntityId);
                }
            }
        }

        Assert.True(sawAttack, "The battle never produced an attack event.");
    }

    // ----- Legacy presets leave Itak footwork state untouched -----

    /// <summary>
    /// The Itak counterpart of
    /// <c>MovementPipelineIntegrationTests.ALegacyPresetRunsNoEquipmentStageAtAll</c>:
    /// under every registered legacy movement preset, a battle whose
    /// warriors carry both Itak rows never writes any of the five
    /// equipment-relative fields. The rows exist in the profile tables, but
    /// only <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> may
    /// read them.
    /// </summary>
    [Theory]
    [InlineData(MovementPresetId.IndependentPursuitV1)]
    [InlineData(MovementPresetId.PersistentContingentsV2)]
    [InlineData(MovementPresetId.PersistentContingentsV3)]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.PersistentContingentsV5)]
    public void EveryLegacyPresetLeavesItakFootworkStateUntouched(
        MovementPresetId legacyPreset)
    {
        var scenario = CreateScenario() with { MovementPreset = legacyPreset };
        var agents = new[]
        {
            CreateAgent(1, factionId: 0, 92_160, 51_200, scenario, SoloItak),
            CreateAgent(
                2, factionId: 0, 92_160, 55_296, scenario, ShieldedItak),
            CreateAgent(3, factionId: 1, 112_640, 51_200, scenario, SoloItak),
            CreateAgent(
                4, factionId: 1, 112_640, 55_296, scenario, ShieldedItak),
        };
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);

        for (var tick = 0; tick < 20; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.All(agents, agent =>
        {
            Assert.Equal(Facing16.None, agent.Facing);
            Assert.Equal(0, agent.MovementPaceRaw);
            Assert.Equal(TacticalPosture.None, agent.TacticalPosture);
            Assert.Equal(FootworkPhase.None, agent.FootworkPhase);
            Assert.Equal(0, agent.FootworkTicksRemaining);
        });
    }

    // ----- Itak-heavy long-run determinism -----

    /// <summary>
    /// The Itak-heavy counterpart of
    /// <c>MovementPipelineIntegrationTests.TwoIdenticalVSixBattlesStayHashIdenticalTickForTick</c>,
    /// over a substantially longer run: forty warriors split evenly between
    /// the solo and shielded Itak roster entries, two simulations from the
    /// same scenario advanced in lockstep for five hundred ticks, state
    /// hash and outcome equal on every single tick.
    /// </summary>
    [Fact]
    public void TwoIdenticalItakHeavyVSixBattlesStayHashIdenticalTickForTick()
    {
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 40) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
            RosterCounts = ImmutableArray.Create(0, 0, 0, 0, 10, 10),
        };
        var first = BattleSimulation.Create(scenario);
        var second = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 500; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();
            Assert.Equal(first.ComputeStateHash(), second.ComputeStateHash());
            Assert.Equal(first.Outcome, second.Outcome);
        }
    }

    // ----- Curated count-boundary disengagement -----

    /// <summary>
    /// One solo Itak against two, observed through a whole battle: two
    /// enemies against the actor alone inside the support radius sit past
    /// the solo entry ratio (2 &#215; 10,000 &#8805; 1 &#215; 12,500), so
    /// the actor disengages. A live but distant ally keeps the global
    /// headcount at two against two — posture Hold, never Withdraw or
    /// Yield, asserted every tick — so the observed entry can only be the
    /// support-ratio step, not the unconditional posture step. After entry
    /// the actor attempts the exit: its distance to the nearest enemy does
    /// not monotonically shrink, the observable form of an opened exit.
    /// </summary>
    [Fact]
    public void AnOutnumberedSoloItakEntersDisengageAndOpensItsExitOneAgainstTwo()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1, factionId: 0, 51_200, 51_200, scenario, SoloItak);
        var farAlly = CreateAgent(
            2, factionId: 0, 10_240, 10_240, scenario, SoloItak);
        var firstEnemy = CreateAgent(
            3, factionId: 1, 55_296, 50_176, scenario, SoloItak);
        var secondEnemy = CreateAgent(
            4, factionId: 1, 55_296, 52_224, scenario, SoloItak);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, farAlly, firstEnemy, secondEnemy);

        var entrySeen = false;
        var postEntryDistances = new List<long>();
        for (var tick = 0; tick < 60; tick++)
        {
            actor.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();

            Assert.NotEqual(TacticalPosture.Withdraw, actor.TacticalPosture);
            Assert.NotEqual(TacticalPosture.Yield, actor.TacticalPosture);
            entrySeen |= actor.FootworkPhase == FootworkPhase.Disengage;
            if (entrySeen)
            {
                postEntryDistances.Add(Math.Min(
                    SquaredDistance(actor, firstEnemy),
                    SquaredDistance(actor, secondEnemy)));
            }
        }

        Assert.True(entrySeen, "The outnumbered actor never disengaged.");
        Assert.True(
            postEntryDistances.Count >= 2,
            "The entry left no post-entry ticks to observe.");
        var sawNonShrinkingStep = false;
        for (var index = 1; index < postEntryDistances.Count; index++)
        {
            sawNonShrinkingStep |=
                postEntryDistances[index] >= postEntryDistances[index - 1];
        }

        Assert.True(
            sawNonShrinkingStep,
            "The actor's distance to the nearest enemy shrank on every " +
            "post-entry tick; the exit never opened.");
    }

    /// <summary>
    /// The shielded entry boundary observed through a whole battle at its
    /// exact equality: three enemies against a pair of shielded Itak allies
    /// inside the support radius lands the ratio precisely on the entry
    /// threshold (3 &#215; 10,000 = 2 &#215; 15,000), and equality enters.
    /// Two live but distant allies hold the global headcount at four
    /// against three — posture never Withdraw or Yield, asserted every
    /// tick — so the observed entry can only be the ratio step. Were any
    /// one of the three enemies outside the support radius, the ratio would
    /// sit below entry and no branch would disengage.
    /// </summary>
    [Fact]
    public void AShieldedItakPairEntersDisengageAtTheExactTwoAgainstThreeEquality()
    {
        var scenario = CreateScenario();
        var firstAlly = CreateAgent(
            1, factionId: 0, 51_200, 50_176, scenario, ShieldedItak);
        var secondAlly = CreateAgent(
            2, factionId: 0, 51_200, 52_224, scenario, ShieldedItak);
        var farAllies = new[]
        {
            CreateAgent(
                3, factionId: 0, 10_240, 10_240, scenario, ShieldedItak),
            CreateAgent(
                4, factionId: 0, 12_288, 10_240, scenario, ShieldedItak),
        };
        var enemies = new[]
        {
            CreateAgent(
                5, factionId: 1, 55_296, 49_152, scenario, ShieldedItak),
            CreateAgent(
                6, factionId: 1, 55_296, 51_200, scenario, ShieldedItak),
            CreateAgent(
                7, factionId: 1, 55_296, 53_248, scenario, ShieldedItak),
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario, [firstAlly, secondAlly, .. farAllies, .. enemies]);

        var firstEntered = false;
        var secondEntered = false;
        for (var tick = 0; tick < 40; tick++)
        {
            firstAlly.AttackCooldownRemaining = 100;
            secondAlly.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();

            Assert.NotEqual(
                TacticalPosture.Withdraw, firstAlly.TacticalPosture);
            Assert.NotEqual(TacticalPosture.Yield, firstAlly.TacticalPosture);
            Assert.NotEqual(
                TacticalPosture.Withdraw, secondAlly.TacticalPosture);
            Assert.NotEqual(TacticalPosture.Yield, secondAlly.TacticalPosture);
            firstEntered |= firstAlly.FootworkPhase == FootworkPhase.Disengage;
            secondEntered |=
                secondAlly.FootworkPhase == FootworkPhase.Disengage;
        }

        Assert.True(firstEntered, "The first ally never disengaged.");
        Assert.True(secondEntered, "The second ally never disengaged.");
    }

    /// <summary>
    /// Three against five sits past both rows' entry ratios (5 &#215;
    /// 10,000 &#8805; 3 &#215; 12,500 solo, 5 &#215; 10,000 &#8805; 3
    /// &#215; 15,000 shielded), so every member of the outnumbered trio
    /// disengages on either row. Two live but distant allies level the
    /// global headcount at five against five — posture never Withdraw or
    /// Yield, asserted every tick — so the observed entries can only be the
    /// ratio step.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothItakRowsEnterDisengageThreeAgainstFive(bool shielded)
    {
        var scenario = CreateScenario();
        var loadout = shielded ? ShieldedItak : SoloItak;
        var trio = new[]
        {
            CreateAgent(1, factionId: 0, 51_200, 49_664, scenario, loadout),
            CreateAgent(2, factionId: 0, 51_200, 51_200, scenario, loadout),
            CreateAgent(3, factionId: 0, 51_200, 52_736, scenario, loadout),
        };
        var farAllies = new[]
        {
            CreateAgent(4, factionId: 0, 10_240, 10_240, scenario, loadout),
            CreateAgent(5, factionId: 0, 12_288, 10_240, scenario, loadout),
        };
        var enemies = new[]
        {
            CreateAgent(6, factionId: 1, 54_272, 48_128, scenario, loadout),
            CreateAgent(7, factionId: 1, 54_272, 49_664, scenario, loadout),
            CreateAgent(8, factionId: 1, 54_272, 51_200, scenario, loadout),
            CreateAgent(9, factionId: 1, 54_272, 52_736, scenario, loadout),
            CreateAgent(10, factionId: 1, 54_272, 54_272, scenario, loadout),
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario, [.. trio, .. farAllies, .. enemies]);

        var entered = new bool[trio.Length];
        for (var tick = 0; tick < 40; tick++)
        {
            foreach (var member in trio)
            {
                member.AttackCooldownRemaining = 100;
            }

            simulation.AdvanceOneTick();

            for (var index = 0; index < trio.Length; index++)
            {
                Assert.NotEqual(
                    TacticalPosture.Withdraw, trio[index].TacticalPosture);
                Assert.NotEqual(
                    TacticalPosture.Yield, trio[index].TacticalPosture);
                entered[index] |=
                    trio[index].FootworkPhase == FootworkPhase.Disengage;
            }
        }

        for (var index = 0; index < entered.Length; index++)
        {
            Assert.True(
                entered[index],
                $"Trio member {index} never disengaged on the " +
                $"{(shielded ? "shielded" : "solo")} row.");
        }
    }

    // ----- Helpers -----

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
