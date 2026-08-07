using System.Collections.Immutable;

using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Tasks K3, K4, and K5 of the kalis movement plan: the twelve directed
/// Kalis-variant 1v1 cells, the focused geometry cases, the explicit
/// combat-V2 roster scenarios that keep shielded Kalis reachable, and the
/// Kalis-specific hash, snapshot, and legacy-preset neutrality coverage.
/// </summary>
/// <remarks>
/// <para>
/// Every scenario here names its combat preset explicitly. Shielded Kalis
/// exists only under <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>,
/// the one preset fielding all six canonical loadouts, so the duel cells
/// select it uniformly rather than switching per cell — nothing in this file
/// ever relies on the shipped default. Every assertion is a movement
/// property: a distance band, a lane, a phase, a pace ceiling, or a
/// determinism equality. No cell asserts a winner, and no cell asserts an
/// equal win rate; balance here means role viability, which these tests do
/// not attempt to measure.
/// </para>
/// <para>
/// The 176 mechanically selected Kalis-relevant 2v2 cells of the shared
/// scenario matrix used to run here as well, under a caller-order and
/// pace-ceiling contract. The Itak and tall-hardwood-shield suites each held
/// an overlapping 176-cell slice of the same 231-cell matrix under contracts
/// of their own, so most cells ran two or three times and each run applied
/// only part of the union. Every cell now runs exactly once, under the union
/// of all three contracts — including this suite's caller-order reversal — in
/// <see cref="MovementMatrixContractTests"/>. The twelve directed duel cells
/// stay here, because they assert Kalis liveness rather than matrix
/// determinism: the warrior closes into its own band, works the whole
/// approach-engage-commit-recover lifecycle, and lands a blow.
/// </para>
/// </remarks>
public sealed class KalisMovementScenarioTests
{
    /// <summary>Canonical loadout index of solo Kalis, <c>KA</c>.</summary>
    private const int SoloKalisIndex = 2;

    /// <summary>
    /// Canonical loadout index of Kalis plus Tall Hardwood, <c>KS</c>.
    /// </summary>
    private const int ShieldedKalisIndex = 4;

    private const int AttackRangeRaw = 5 * FixedPoint.Scale;

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV6);

    // ----- K3 step 1: the twelve directed 1v1 cells -----

    public static TheoryData<int, int> KalisOneVersusOneCells()
    {
        var data = new TheoryData<int, int>();
        foreach (var kalis in new[] { SoloKalisIndex, ShieldedKalisIndex })
        {
            for (var opponent = 0;
                opponent < MovementScenarioMatrix.CanonicalLoadoutCount;
                opponent++)
            {
                data.Add(kalis, opponent);
            }
        }

        return data;
    }

    /// <summary>
    /// Each Kalis variant against each canonical opponent, from a common
    /// opening 10,240 raw apart. The assertions are movement properties, not
    /// outcomes: the warrior closes into its own offset-adjusted preferred
    /// band, works through the approach, engage, and commitment lifecycle,
    /// keeps a stable target, and never exceeds its own forward pace cap.
    /// The final assertion is the plan's rejected failure mode stated
    /// positively — no Kalis geometry becomes an indefinite no-contact orbit
    /// inside the bounded window.
    /// </summary>
    [Theory]
    [MemberData(nameof(KalisOneVersusOneCells))]
    public void EveryDirectedKalisDuelClosesEngagesAndCommits(
        int kalisIndex, int opponentIndex)
    {
        var kalisLoadout =
            MovementScenarioMatrix.CanonicalLoadouts[kalisIndex];
        var opponentLoadout =
            MovementScenarioMatrix.CanonicalLoadouts[opponentIndex];
        var profile = V6.ResolveLoadoutProfile(kalisLoadout);
        var preferredRaw = MovementRouteRules.EffectivePreferredDistanceRaw(
            AttackRangeRaw, profile, opponentIndex);
        var forwardCapRaw = MovementRouteRules.DesiredPaceRaw(
            MovementSpeedRaw, profile.ForwardPaceBasisPoints);

        var scenario = CreateScenario();
        var kalis = CreateAgent(
            1, factionId: 0, 92_160, 51_200, scenario, kalisLoadout);
        var opponent = CreateAgent(
            2, factionId: 1, 102_400, 51_200, scenario, opponentLoadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, kalis, opponent);

        var reachedPreferredBand = false;
        var phasesSeen = new HashSet<FootworkPhase>();
        var attacks = 0;

        for (var tick = 0; tick < 400; tick++)
        {
            simulation.AdvanceOneTick();

            Assert.True(
                kalis.MovementPaceRaw <= forwardCapRaw,
                $"Kalis retained pace {kalis.MovementPaceRaw} above its own " +
                $"forward cap {forwardCapRaw} on tick {simulation.Tick}.");

            phasesSeen.Add(kalis.FootworkPhase);
            attacks += simulation.LastEvents.Count(
                battleEvent => battleEvent.Kind == BattleEventKind.Attack);

            var separationSquared = CollisionGeometry.SquaredDistance(
                kalis.XRaw, kalis.YRaw, opponent.XRaw, opponent.YRaw);
            reachedPreferredBand |=
                separationSquared <= preferredRaw * preferredRaw;

            Assert.Equal(opponent.EntityId, kalis.TargetEntityId);
        }

        Assert.True(
            reachedPreferredBand,
            "The Kalis warrior never reached its preferred band, so this " +
            "duel is an indefinite no-contact orbit.");
        Assert.Contains(FootworkPhase.Approach, phasesSeen);
        Assert.Contains(FootworkPhase.Engage, phasesSeen);
        Assert.Contains(FootworkPhase.Commit, phasesSeen);
        Assert.Contains(FootworkPhase.Recover, phasesSeen);
        Assert.True(attacks > 0, "The duel produced no attack at all.");
    }

    // ----- K3 step 5: focused geometry -----

    /// <summary>
    /// Two Kalis allies approaching the same pair of enemies take distinct
    /// lanes rather than the same one: their tick-one endpoints differ, and
    /// both keep at least their own ally-clearance radius from each other.
    /// </summary>
    [Theory]
    [InlineData(SoloKalisIndex)]
    [InlineData(ShieldedKalisIndex)]
    public void TwoKalisAlliesTakeSeparateLanes(int kalisIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[kalisIndex];
        var profile = V6.ResolveLoadoutProfile(loadout);
        var clearanceRaw = MovementRouteRules.ClearanceRadiusRaw(
            BodyRadiusRaw, profile.AllyClearanceBodyDiametersBasisPoints);

        var scenario = CreateScenario();
        var north = CreateAgent(
            1, factionId: 0, 92_160, 49_152, scenario, loadout);
        var south = CreateAgent(
            2, factionId: 0, 92_160, 53_248, scenario, loadout);
        var enemyNorth = CreateAgent(
            3, factionId: 1, 102_400, 49_152, scenario, loadout);
        var enemySouth = CreateAgent(
            4, factionId: 1, 102_400, 53_248, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, north, south, enemyNorth, enemySouth);

        for (var tick = 0; tick < 40; tick++)
        {
            simulation.AdvanceOneTick();

            Assert.True(
                north.XRaw != south.XRaw || north.YRaw != south.YRaw,
                $"Both allies resolved the same position on tick " +
                $"{simulation.Tick}.");
            Assert.True(
                CollisionGeometry.SquaredDistance(
                    north.XRaw, north.YRaw, south.XRaw, south.YRaw) >=
                    clearanceRaw * clearanceRaw,
                $"The allies closed inside their own clearance radius on " +
                $"tick {simulation.Tick}.");
        }

        Assert.True(north.XRaw > 92_160 && south.XRaw > 92_160);
    }

    /// <summary>
    /// An ally standing directly in the only lane refuses the approach
    /// rather than walking through it: the direct 307-unit step lands 593
    /// raw from the ally and both 22.5-degree obliques land 628 raw from it,
    /// all strictly inside the 1228-raw solo Kalis clearance radius, so no
    /// candidate survives and the approach finalises <c>Refuse</c> with no
    /// movement and no retained pace.
    /// </summary>
    [Fact]
    public void AKalisApproachWithEveryLaneBlockedRefuses()
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[SoloKalisIndex];
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1, factionId: 0, 51_200, 51_200, scenario, loadout);
        var allyAhead = CreateAgent(
            2, factionId: 0, 52_100, 51_200, scenario, loadout);
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

    /// <summary>
    /// An ally's death is reassessed rather than cached. The control run
    /// keeps both allies alive and shows that two Kalis warriors against two
    /// enemies sit below the disengagement entry on both rows; the
    /// experimental run is the same geometry with a one-hit-point ally, and
    /// once that ally falls the survivor's own support ratio has crossed the
    /// entry. The actor's cooldown is pinned every tick in both runs, so its
    /// own attack lifecycle — which outranks the ratio steps — never masks
    /// the transition.
    /// </summary>
    [Theory]
    [InlineData(SoloKalisIndex)]
    [InlineData(ShieldedKalisIndex)]
    public void AnAllyDeathIsReassessedRatherThanCached(int kalisIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[kalisIndex];

        var control = BuildAllyDeathCase(loadout, allyHitPoints: null);
        for (var tick = 0; tick < 10; tick++)
        {
            control.Actor.AttackCooldownRemaining = 100;
            control.Simulation.AdvanceOneTick();

            Assert.True(control.Ally.IsAlive);
            Assert.NotEqual(
                FootworkPhase.Disengage, control.Actor.FootworkPhase);
        }

        var experiment = BuildAllyDeathCase(loadout, allyHitPoints: 1);
        for (var tick = 0; tick < 40 && experiment.Ally.IsAlive; tick++)
        {
            experiment.Actor.AttackCooldownRemaining = 100;
            experiment.Simulation.AdvanceOneTick();
        }

        Assert.False(experiment.Ally.IsAlive);

        experiment.Actor.AttackCooldownRemaining = 100;
        experiment.Simulation.AdvanceOneTick();

        Assert.Equal(
            FootworkPhase.Disengage, experiment.Actor.FootworkPhase);
    }

    private static (
        BattleSimulation Simulation, AgentState Actor, AgentState Ally)
        BuildAllyDeathCase(CombatLoadout loadout, int? allyHitPoints)
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1, factionId: 0, 92_160, 51_200, scenario, loadout);
        var ally = CreateAgent(
            2, factionId: 0, 92_160, 55_296, scenario, loadout,
            maximumHitPoints: allyHitPoints);
        var enemyNear = CreateAgent(
            3, factionId: 1, 93_184, 51_200, scenario, loadout);
        var enemyFar = CreateAgent(
            4, factionId: 1, 93_184, 55_296, scenario, loadout);

        return (
            BattleSimulation.CreateForTesting(
                scenario, actor, ally, enemyNear, enemyFar),
            actor,
            ally);
    }

    // ----- K4: shielded Kalis under explicit combat V2 -----

    /// <summary>
    /// Combat preset V2's roster order is <c>KP, WA, KA solo, KA shielded,
    /// IT solo, IT shielded</c>, so a shielded-Kalis-only battle names
    /// roster index 3 and a solo-Kalis-only battle names index 2. Both are
    /// stated explicitly; neither relies on the round-robin assignment the
    /// empty default would use.
    /// </summary>
    [Theory]
    [InlineData(3, ShieldId.TallHardwood)]
    [InlineData(2, ShieldId.None)]
    public void AnExplicitVTwoRosterFieldsOnlyTheRequestedKalisLoadout(
        int rosterIndex, ShieldId shield)
    {
        const int AgentsPerFaction = 4;
        var counts = new int[6];
        counts[rosterIndex] = AgentsPerFaction;
        var scenario = CreateDeployedScenario(AgentsPerFaction, counts);
        var simulation = BattleSimulation.Create(scenario);

        Assert.Equal(AgentsPerFaction * 2, simulation.Agents.Count);
        Assert.All(simulation.Agents, view =>
        {
            Assert.Equal(WeaponId.Kalis, view.Loadout.Weapon);
            Assert.Equal(ArmorId.LightOrganic, view.Loadout.Armor);
            Assert.Equal(shield, view.Loadout.Shield);
        });
    }

    /// <summary>
    /// Every shielded Kalis warrior deployed through the explicit V2 roster
    /// resolves the Tall Hardwood <c>KS</c> row, and never the solo row —
    /// the shielded key does not fall back.
    /// </summary>
    [Fact]
    public void EveryDeployedShieldedKalisResolvesTheShieldedRow()
    {
        var scenario = CreateDeployedScenario(4, [0, 0, 0, 4, 0, 0]);
        var simulation = BattleSimulation.Create(scenario);

        Assert.All(simulation.Agents, view =>
        {
            Assert.Same(
                TallHardwoodMovementProfiles.KalisRow,
                V6.ResolveLoadoutProfile(view.Loadout));
            Assert.NotSame(
                KalisMovementProfile.Row,
                V6.ResolveLoadoutProfile(view.Loadout));
        });
    }

    /// <summary>
    /// Two identical explicit-V2 runs, solo and shielded, produce the same
    /// ordered event stream, the same state hash on every tick, and the same
    /// outcome.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    public void TwoIdenticalExplicitVTwoRunsAreOrderedIdentical(
        int rosterIndex)
    {
        var counts = new int[6];
        counts[rosterIndex] = 6;
        var scenario = CreateDeployedScenario(6, counts);
        var first = BattleSimulation.Create(scenario);
        var second = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 200; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();

            Assert.Equal(first.ComputeStateHash(), second.ComputeStateHash());
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(
                first.LastEvents.ToArray(), second.LastEvents.ToArray());
        }
    }

    /// <summary>
    /// Shielded Kalis stays out of the solo-only rosters. Combat presets V3
    /// and V4 field four solo loadouts and no shielded one, which is why
    /// every shielded scenario in this file names V2; this test states that
    /// dependency rather than leaving it implicit, and it edits nothing.
    /// </summary>
    [Theory]
    [InlineData(CombatPresetId.PrecolonialPhilippinesV3)]
    [InlineData(CombatPresetId.PrecolonialPhilippinesV4)]
    public void TheSoloOnlyCombatPresetsStillFieldNoShieldedKalis(
        CombatPresetId preset)
    {
        var roster = CombatPresetRegistry.Get(preset).Roster;

        Assert.Equal(4, roster.Count);
        Assert.DoesNotContain(
            roster, loadout => loadout.Shield != ShieldId.None);
        Assert.Contains(
            roster,
            loadout =>
                loadout.Weapon == WeaponId.Kalis &&
                loadout.Shield == ShieldId.None);
    }

    // ----- K5: hash, snapshot, and legacy-preset neutrality -----

    /// <summary>
    /// Every authoritative footwork field of a Kalis warrior reaches the V6
    /// state hash, one field at a time.
    /// </summary>
    [Theory]
    [InlineData(SoloKalisIndex, 0)]
    [InlineData(SoloKalisIndex, 1)]
    [InlineData(SoloKalisIndex, 2)]
    [InlineData(SoloKalisIndex, 3)]
    [InlineData(SoloKalisIndex, 4)]
    [InlineData(ShieldedKalisIndex, 0)]
    [InlineData(ShieldedKalisIndex, 1)]
    [InlineData(ShieldedKalisIndex, 2)]
    [InlineData(ShieldedKalisIndex, 3)]
    [InlineData(ShieldedKalisIndex, 4)]
    public void EachKalisFootworkFieldReachesTheVSixStateHash(
        int kalisIndex, int fieldIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[kalisIndex];
        var scenario = CreateScenario();
        var kalis = CreateAgent(
            1, factionId: 0, 92_160, 51_200, scenario, loadout);
        var enemy = CreateAgent(
            2, factionId: 1, 112_640, 51_200, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, kalis, enemy);
        var baseline = simulation.ComputeStateHash();

        switch (fieldIndex)
        {
            case 0:
                kalis.Facing = Facing16.SouthWest;
                break;
            case 1:
                kalis.MovementPaceRaw = 41;
                break;
            case 2:
                kalis.TacticalPosture = TacticalPosture.Yield;
                break;
            case 3:
                kalis.FootworkPhase = FootworkPhase.Refuse;
                break;
            default:
                kalis.FootworkTicksRemaining = 2;
                break;
        }

        Assert.NotEqual(baseline, simulation.ComputeStateHash());
    }

    /// <summary>
    /// A Kalis warrior's five authoritative footwork fields survive
    /// <see cref="BattleSimulation.CreateSnapshot"/> field for field,
    /// including the commitment timer, whose duration is two ticks on the
    /// solo row and three on the shielded one. The derived local context and
    /// the immutable profile are deliberately absent from the snapshot and
    /// are not asserted here, because nothing may put them there.
    /// </summary>
    [Theory]
    [InlineData(SoloKalisIndex)]
    [InlineData(ShieldedKalisIndex)]
    public void KalisFootworkStateSurvivesTheSnapshotRoundTrip(int kalisIndex)
    {
        var loadout = MovementScenarioMatrix.CanonicalLoadouts[kalisIndex];
        var profile = V6.ResolveLoadoutProfile(loadout);
        var scenario = CreateScenario();
        var west = CreateAgent(
            1, factionId: 0, 92_160, 51_200, scenario, loadout);
        var east = CreateAgent(
            2, factionId: 1, 93_184, 51_200, scenario, loadout);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        simulation.AdvanceOneTick();

        var snapshot = simulation.CreateSnapshot();
        var view = snapshot.Agents.Single(
            candidate => candidate.EntityId == west.EntityId);

        Assert.Equal(FootworkPhase.Commit, view.FootworkPhase);
        Assert.Equal(profile.CommitmentTicks, view.FootworkTicksRemaining);
        Assert.Equal(west.Facing, view.Facing);
        Assert.Equal(west.MovementPaceRaw, view.MovementPaceRaw);
        Assert.Equal(west.TacticalPosture, view.TacticalPosture);
        Assert.Equal(west.FootworkPhase, view.FootworkPhase);
        Assert.Equal(
            west.FootworkTicksRemaining, view.FootworkTicksRemaining);
    }

    /// <summary>
    /// Under every frozen preset, a battle of nothing but Kalis warriors
    /// leaves all five footwork fields at their neutral values: the legacy
    /// proposal path never resolves a Kalis profile and never writes a
    /// footwork field, which is what keeps V1 through V5 replays untouched
    /// by anything this session owns.
    /// </summary>
    [Theory]
    [InlineData(MovementPresetId.IndependentPursuitV1)]
    [InlineData(MovementPresetId.PersistentContingentsV2)]
    [InlineData(MovementPresetId.PersistentContingentsV3)]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.PersistentContingentsV5)]
    public void ALegacyPresetLeavesKalisFootworkStateNeutral(
        MovementPresetId preset)
    {
        var scenario = CreateDeployedScenario(
            6, [0, 0, 3, 3, 0, 0], preset);
        var simulation = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 100; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.All(simulation.CreateSnapshot().Agents, view =>
        {
            Assert.Equal(Facing16.None, view.Facing);
            Assert.Equal(0, view.MovementPaceRaw);
            Assert.Equal(TacticalPosture.None, view.TacticalPosture);
            Assert.Equal(FootworkPhase.None, view.FootworkPhase);
            Assert.Equal(0, view.FootworkTicksRemaining);
        });
    }

    /// <summary>
    /// The same Kalis battle is reproducible under every frozen preset and
    /// under V6, and every preset produces a distinct trajectory: five
    /// legacy digests plus the V6 digest are pairwise different, so a Kalis
    /// row change that leaked into a legacy path would collapse two of them
    /// together.
    /// </summary>
    [Fact]
    public void EveryPresetReproducesItsOwnDistinctKalisTrajectory()
    {
        MovementPresetId[] presets =
        [
            MovementPresetId.IndependentPursuitV1,
            MovementPresetId.PersistentContingentsV2,
            MovementPresetId.PersistentContingentsV3,
            MovementPresetId.PersistentContingentsV4,
            MovementPresetId.PersistentContingentsV5,
            MovementPresetId.EquipmentRelativeFootworkV6,
        ];

        var digests = new List<ulong>();
        foreach (var preset in presets)
        {
            var scenario = CreateDeployedScenario(
                6, [0, 0, 3, 3, 0, 0], preset);
            var first = BattleSimulation.Create(scenario);
            var second = BattleSimulation.Create(scenario);

            for (var tick = 0; tick < 150; tick++)
            {
                first.AdvanceOneTick();
                second.AdvanceOneTick();
            }

            Assert.Equal(first.ComputeStateHash(), second.ComputeStateHash());
            digests.Add(first.ComputeStateHash());
        }

        Assert.Equal(digests.Count, digests.Distinct().Count());
    }

    // ----- Helpers -----

    private const int BodyRadiusRaw = FixedPoint.Scale / 2;

    private const int MovementSpeedRaw = FixedPoint.Scale / 2;

    /// <summary>
    /// The hand-built duel and team scenario. Combat preset
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> is named
    /// explicitly because it is the only preset fielding all six canonical
    /// loadouts.
    /// </summary>
    private static Scenario CreateScenario() =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 2,
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
    /// A fully deployed scenario with an explicit combat-V2 roster count
    /// vector, for the cases that must exercise the real deployment path
    /// rather than hand-placed agents.
    /// </summary>
    private static Scenario CreateDeployedScenario(
        int agentsPerFaction,
        int[] rosterCounts,
        MovementPresetId movementPreset =
            MovementPresetId.EquipmentRelativeFootworkV6) =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 200,
            AgentsPerFaction: agentsPerFaction,
            TickRate: 20,
            TickLimit: 5_000)
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = movementPreset,
            RosterCounts = [.. rosterCounts],
        };

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout,
        int? maximumHitPoints = null) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            maximumHitPoints ?? scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            loadout);
}
