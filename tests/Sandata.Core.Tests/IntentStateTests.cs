using System.Collections.Immutable;
using System.Linq;
using Hukbo.Core.Mathematics;
using Sandata.Core.Determinism;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Decision D1 of the 2026-08-14 "the shipped mission freezes at first contact"
/// design: stage 8's selected intent reaches authoritative state.
/// </summary>
/// <remarks>
/// Before this, <see cref="IntentSelection.SelectAll"/> ran every tick and its
/// results lived only in <see cref="SandataSimulation.PendingIntents"/>, so
/// <see cref="OperatorState.Intent"/> read <see cref="OperatorIntent.Hold"/> for
/// every operator on every tick ever simulated — a field folded into the state
/// hash and carried in the snapshot whose value was a constant.
/// </remarks>
public sealed class IntentStateTests
{
    private static NavGrid NewOpenGrid(int widthCells = 32, int heightCells = 32) =>
        new(widthCells, heightCells);

    private static MissionState BuildState(params (ulong EntityId, int X, int Y, int Faction, int Health)[] operators)
    {
        var built = ImmutableArray.CreateBuilder<OperatorState>(operators.Length);
        foreach (var (entityId, x, y, faction, health) in operators)
        {
            built.Add(new OperatorState(
                EntityId: entityId,
                PositionX: FixedPoint.FromWhole(x),
                PositionY: FixedPoint.FromWhole(y),
                Facing: 0,
                AimAngle: new Sandata.Core.Mathematics.Bam16(0),
                Health: health,
                Faction: faction,
                Intent: 0,
                IsCrouched: false,
                WeaponLowered: false,
                WeaponChainPhase: 0,
                WeaponChainRemainingTicks: 0,
                MagazineRounds: 30,
                CyclicFireAccumulator: 0,
                SuppressionCounter: 0));
        }

        return new MissionState(
            Tick: 0, Phase: 1, Winner: -1,
            NextEntityId: (ulong)(operators.Length + 1), NextEventSequence: 0)
        {
            Operators = built.MoveToImmutable(),
            FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
            Doors = ImmutableArray<DoorState>.Empty,
            Groups = ImmutableArray<GroupPathState>.Empty,
            RngStreams = ImmutableArray<RngStreamState>.Empty,
        };
    }

    private static SandataSimulation NewSimulation(MissionState state)
    {
        var grid = NewOpenGrid();
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var mission = new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1UL,
            mapContentHash: 1UL,
            tickPolicy: new MissionTickPolicy(TickLimit: 1000, StateHashCadenceTicks: 1),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, state.Operators.Count(o => o.Faction == 0)),
                new MissionFactionSetup(1, state.Operators.Count(o => o.Faction == 1))),
            rulesetId: SandataPresetId.ModernTacticalV1);

        return new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state,
            ImmutableArray<CoverRecord>.Empty);
    }

    /// <summary>
    /// The whole defect in one assertion: what stage 8 selected is what state
    /// carries, for every operator, on every tick of a twenty-tick run.
    /// </summary>
    [Fact]
    public void EveryOperatorsStoredIntentEqualsTheIntentStageEightSelected()
    {
        var simulation = NewSimulation(BuildState(
            (1UL, 40, 40, 0, 100),
            (2UL, 44, 40, 1, 100)));

        for (var tick = 0; tick < 20; tick++)
        {
            simulation.RunTick(tick);

            var pending = simulation.PendingIntents;
            var operators = simulation.State.Operators;

            Assert.Equal(operators.Length, pending.Length);
            for (var i = 0; i < operators.Length; i++)
            {
                Assert.Equal((int)pending[i].Intent, operators[i].Intent);
            }
        }
    }

    /// <summary>
    /// Two hostiles within identify range of each other engage, and the state
    /// says so. This is the case the shipped mission produced and the state
    /// denied for the whole of every run before D1.
    /// </summary>
    [Fact]
    public void AnOperatorWithAnIdentifiedHostileCarriesEngageInState()
    {
        var simulation = NewSimulation(BuildState(
            (1UL, 40, 40, 0, 100),
            (2UL, 60, 40, 1, 100)));

        simulation.RunTick(0);

        Assert.Contains(
            simulation.State.Operators,
            op => op.Intent == (int)OperatorIntent.Engage);
    }

    /// <summary>
    /// A dead operator carries <see cref="OperatorIntent.Dead"/> rather than
    /// the <see cref="OperatorIntent.Hold"/> every corpse used to carry.
    /// </summary>
    [Fact]
    public void ADeadOperatorCarriesDeadInState()
    {
        var simulation = NewSimulation(BuildState(
            (1UL, 40, 40, 0, 0),
            (2UL, 200, 200, 1, 100)));

        simulation.RunTick(0);

        var dead = simulation.State.Operators.Single(op => op.EntityId == 1UL);
        Assert.Equal((int)OperatorIntent.Dead, dead.Intent);
    }

    /// <summary>
    /// The field is load-bearing in the digest, so a run whose intents differ
    /// is a run whose state hash differs. Without this, writing the field would
    /// be invisible to every determinism test in the suite.
    /// </summary>
    [Fact]
    public void TwoStatesDifferingOnlyInIntentHashDifferently()
    {
        var holding = BuildState((1UL, 40, 40, 0, 100));
        var engaging = holding with
        {
            Operators = ImmutableArray.Create(
                holding.Operators[0] with { Intent = (int)OperatorIntent.Engage }),
        };

        var ruleset = SandataRuleset.ModernTacticalV1;
        var mission = new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: 1UL,
            mapContentHash: 1UL,
            tickPolicy: new MissionTickPolicy(TickLimit: 1000, StateHashCadenceTicks: 1),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(0, 1),
                new MissionFactionSetup(1, 1)),
            rulesetId: SandataPresetId.ModernTacticalV1);

        Assert.NotEqual(
            SandataStateHasher.Compute(mission, holding, ruleset),
            SandataStateHasher.Compute(mission, engaging, ruleset));
    }
}
