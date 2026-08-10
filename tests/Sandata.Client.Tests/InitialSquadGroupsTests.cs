using System.Collections.Immutable;
using System.Linq;
using Hukbo.Core.Mathematics;
using Sandata.Client.Simulation;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Simulation;

namespace Sandata.Client.Tests;

/// <summary>
/// <see cref="InitialSquadGroups"/>'s bar: Sandata's autonomous destination
/// source produces the group identities <c>SquadGrouping</c> will derive on
/// tick zero, gives every assaulting group a real objective cell, and gives
/// the holding faction nothing.
/// </summary>
/// <remarks>
/// The grid below is 32 by 32 cells at <see cref="NavGrid.CellSizeWu"/> of 4,
/// so it spans 128 by 128 world units and a world point (x, y) lands in cell
/// index <c>((y / 4) * 32) + (x / 4)</c>. Every expected cell index in this
/// file is worked out by that arithmetic and written as a literal, rather than
/// recomputed through the same <c>NavGrid</c> calls the code under test uses —
/// an expectation derived from the implementation's own helper is not an
/// oracle for it.
/// </remarks>
public sealed class InitialSquadGroupsTests
{
    private const int GridCells = 32;
    private const int CohesionRadiusWu = 96;
    private const int AssaultingFaction = 0;

    [Fact]
    public void Build_TwoAssaultingOperatorsInsideTheCohesionRadius_ProduceOneGroupIdentifiedByTheLowerEntityId()
    {
        var groups = InitialSquadGroups.Build(
            Roster(
                Operator(entityId: 3, worldX: 8, worldY: 8, faction: AssaultingFaction),
                Operator(entityId: 7, worldX: 20, worldY: 8, faction: AssaultingFaction)),
            Objectives(Objective(index: 0, worldX: 40, worldY: 24)),
            OpenGrid(),
            CohesionRadiusWu,
            AssaultingFaction);

        var group = Assert.Single(groups);

        // 3, not 7: a component's identity is the minimum entity id it
        // contains (design section 8), and the whole point of matching it is
        // that stage 6 will independently derive the same number on tick zero.
        Assert.Equal(3UL, group.GroupId);

        // The leader is entity 3 at (8, 8): cell (2, 2), index 2 * 32 + 2.
        Assert.Equal(66, group.StartCellIndex);

        // The objective at (40, 24): cell (10, 6), index 6 * 32 + 10.
        Assert.Equal(202, group.GoalCellIndex);
        Assert.Equal(202, group.DestinationCellIndex);

        Assert.True(group.HasOutstandingRequest);
        Assert.Equal(0, group.RequestTick);
    }

    // What this test does not bind: the direction of the union-find merge.
    // Inverting InitialSquadGroups.Union's comparison, so the higher index
    // survives as the root, leaves every assertion above passing — the group
    // id comes from FirstIndexOfRoot's ascending scan, not from which index
    // became the root. The break that does bind it is reversing that scan,
    // which fails this test alone with a group id of 7.

    /// <summary>
    /// The cohesion radius is load-bearing and not decorative: the same two
    /// operators, moved apart, stop being one squad and become two, each
    /// seeking its own objective.
    /// </summary>
    [Fact]
    public void Build_TwoAssaultingOperatorsBeyondTheCohesionRadius_ProduceTwoGroups()
    {
        var groups = InitialSquadGroups.Build(
            Roster(
                Operator(entityId: 3, worldX: 8, worldY: 8, faction: AssaultingFaction),
                Operator(entityId: 7, worldX: 120, worldY: 120, faction: AssaultingFaction)),
            Objectives(
                Objective(index: 0, worldX: 40, worldY: 24),
                Objective(index: 1, worldX: 100, worldY: 60)),
            OpenGrid(),
            CohesionRadiusWu,
            AssaultingFaction);

        Assert.Equal(2, groups.Length);
        Assert.Equal([3UL, 7UL], groups.Select(group => group.GroupId));

        // Rank 0 takes objective index 0 at (40, 24); rank 1 takes objective
        // index 1 at (100, 60): cell (25, 15), index 15 * 32 + 25.
        Assert.Equal(202, groups[0].GoalCellIndex);
        Assert.Equal(505, groups[1].GoalCellIndex);
    }

    /// <summary>
    /// Objectives are ranked by their own <c>Index</c> field, not by the order
    /// they happened to appear in the map file. A map that lists index 1 first
    /// must still send the first squad to index 0.
    /// </summary>
    [Fact]
    public void Build_ObjectivesOutOfOrderInTheMap_AreRankedByIndexAndNotByMapOrder()
    {
        var groups = InitialSquadGroups.Build(
            Roster(
                Operator(entityId: 3, worldX: 8, worldY: 8, faction: AssaultingFaction),
                Operator(entityId: 7, worldX: 120, worldY: 120, faction: AssaultingFaction)),
            Objectives(
                Objective(index: 1, worldX: 100, worldY: 60),
                Objective(index: 0, worldX: 40, worldY: 24)),
            OpenGrid(),
            CohesionRadiusWu,
            AssaultingFaction);

        Assert.Equal(2, groups.Length);
        Assert.Equal(202, groups[0].GoalCellIndex);
        Assert.Equal(505, groups[1].GoalCellIndex);
    }

    /// <summary>
    /// Two operators of opposing factions standing on top of each other are
    /// never one squad, and only the assaulting side is given anywhere to go.
    /// </summary>
    [Fact]
    public void Build_OperatorsOfDifferentFactions_AreNeverUnionedAndOnlyTheAssaultingSideSeeks()
    {
        var groups = InitialSquadGroups.Build(
            Roster(
                Operator(entityId: 1, worldX: 8, worldY: 8, faction: AssaultingFaction),
                Operator(entityId: 2, worldX: 9, worldY: 8, faction: 1)),
            Objectives(Objective(index: 0, worldX: 40, worldY: 24)),
            OpenGrid(),
            CohesionRadiusWu,
            AssaultingFaction);

        var group = Assert.Single(groups);
        Assert.Equal(1UL, group.GroupId);
    }

    [Fact]
    public void Build_MoreGroupsThanObjectives_WrapsAroundRatherThanLeavingASquadWithNoDestination()
    {
        var groups = InitialSquadGroups.Build(
            Roster(
                Operator(entityId: 1, worldX: 8, worldY: 8, faction: AssaultingFaction),
                Operator(entityId: 2, worldX: 120, worldY: 8, faction: AssaultingFaction),
                Operator(entityId: 3, worldX: 8, worldY: 120, faction: AssaultingFaction)),
            Objectives(Objective(index: 0, worldX: 40, worldY: 24)),
            OpenGrid(),
            CohesionRadiusWu,
            AssaultingFaction);

        Assert.Equal(3, groups.Length);
        Assert.All(groups, group => Assert.Equal(202, group.GoalCellIndex));
        Assert.All(groups, group => Assert.True(group.HasOutstandingRequest));
    }

    /// <summary>
    /// An objective whose own cell bakes as <see cref="NavCellFlags.Blocked"/>
    /// — a marker placed against a wall, which the inflation pass in
    /// <c>NavBake</c> readily produces — resolves to a nearby open cell rather
    /// than handing the search a goal it must reject.
    /// </summary>
    [Fact]
    public void Build_AnObjectiveOnABlockedCell_ResolvesToANearbyOpenCellInstead()
    {
        var grid = OpenGrid();

        // Block the objective's own cell (10, 6) and the whole ring around it,
        // so a correct answer has to be found two rings out and a test that
        // merely returned "some open cell" cannot pass by luck.
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                grid.Passability[((6 + offsetY) * GridCells) + 10 + offsetX] = NavCellFlags.Blocked;
            }
        }

        var groups = InitialSquadGroups.Build(
            Roster(Operator(entityId: 1, worldX: 8, worldY: 8, faction: AssaultingFaction)),
            Objectives(Objective(index: 0, worldX: 40, worldY: 24)),
            grid,
            CohesionRadiusWu,
            AssaultingFaction);

        var group = Assert.Single(groups);
        Assert.NotEqual(202, group.GoalCellIndex);
        Assert.Equal(NavCellFlags.Open, grid.Passability[group.GoalCellIndex]);

        // Two rings out at most: the resolved cell is inside the 5x5 block
        // centred on (10, 6), which is columns 8 through 12 and rows 4
        // through 8.
        var resolvedX = group.GoalCellIndex % GridCells;
        var resolvedY = group.GoalCellIndex / GridCells;
        Assert.InRange(resolvedX, 8, 12);
        Assert.InRange(resolvedY, 4, 8);
    }

    [Fact]
    public void Build_AGridWithNoOpenCellAtAll_ProducesNoGroupRatherThanAnUnreachableOne()
    {
        var groups = InitialSquadGroups.Build(
            Roster(Operator(entityId: 1, worldX: 8, worldY: 8, faction: AssaultingFaction)),
            Objectives(Objective(index: 0, worldX: 40, worldY: 24)),
            new NavGrid(GridCells, GridCells),
            CohesionRadiusWu,
            AssaultingFaction);

        Assert.Empty(groups);
    }

    [Fact]
    public void Build_NoObjectiveOrNoAssaultingOperator_ProducesNothing()
    {
        var roster = Roster(Operator(entityId: 1, worldX: 8, worldY: 8, faction: AssaultingFaction));
        var objectives = Objectives(Objective(index: 0, worldX: 40, worldY: 24));

        Assert.Empty(InitialSquadGroups.Build(
            roster, ImmutableArray<ObjectiveRecord>.Empty, OpenGrid(), CohesionRadiusWu, AssaultingFaction));

        Assert.Empty(InitialSquadGroups.Build(
            ImmutableArray<OperatorState>.Empty, objectives, OpenGrid(), CohesionRadiusWu, AssaultingFaction));

        // A roster of nothing but defenders: every operator is present, none
        // of them assaults, so nothing seeks.
        Assert.Empty(InitialSquadGroups.Build(
            Roster(Operator(entityId: 1, worldX: 8, worldY: 8, faction: 1)),
            objectives,
            OpenGrid(),
            CohesionRadiusWu,
            AssaultingFaction));
    }

    /// <summary>
    /// <c>MissionState.Groups</c> is documented as ascending by
    /// <c>GroupPathState.GroupId</c>, and <c>SandataStateHasher</c> folds it in
    /// stored order — so an unsorted array would be a hash-visible defect
    /// rather than a cosmetic one.
    /// </summary>
    [Fact]
    public void Build_ManySeparateGroups_ComeBackAscendingByGroupId()
    {
        var groups = InitialSquadGroups.Build(
            Roster(
                Operator(entityId: 2, worldX: 8, worldY: 8, faction: AssaultingFaction),
                Operator(entityId: 5, worldX: 120, worldY: 8, faction: AssaultingFaction),
                Operator(entityId: 9, worldX: 8, worldY: 120, faction: AssaultingFaction),
                Operator(entityId: 11, worldX: 120, worldY: 120, faction: AssaultingFaction)),
            Objectives(Objective(index: 0, worldX: 40, worldY: 24)),
            OpenGrid(),
            CohesionRadiusWu,
            AssaultingFaction);

        Assert.Equal([2UL, 5UL, 9UL, 11UL], groups.Select(group => group.GroupId));
    }

    private static NavGrid OpenGrid()
    {
        // NavGrid's Passability array defaults to all zeros, and zero is
        // NavCellFlags.Blocked — an all-open grid has to be filled explicitly.
        var grid = new NavGrid(GridCells, GridCells);
        for (var index = 0; index < grid.CellCount; index++)
        {
            grid.Passability[index] = NavCellFlags.Open;
        }

        return grid;
    }

    private static ImmutableArray<OperatorState> Roster(params OperatorState[] operators) =>
        [.. operators];

    private static ImmutableArray<ObjectiveRecord> Objectives(params ObjectiveRecord[] objectives) =>
        [.. objectives];

    private static ObjectiveRecord Objective(int index, int worldX, int worldY) =>
        new(LineNumber: index + 1, Index: index, X: worldX, Y: worldY, RadiusWu: 48);

    private static OperatorState Operator(ulong entityId, int worldX, int worldY, int faction)
    {
        var facing = new Bam16(0);
        return new OperatorState(
            EntityId: entityId,
            PositionX: FixedPoint.FromWhole(worldX),
            PositionY: FixedPoint.FromWhole(worldY),
            Facing: facing.ToFacing16(),
            AimAngle: facing,
            Health: 100,
            Faction: faction,
            Intent: 0,
            IsCrouched: false,
            WeaponLowered: false,
            WeaponChainPhase: 0,
            WeaponChainRemainingTicks: 0,
            MagazineRounds: 30,
            CyclicFireAccumulator: 0,
            SuppressionCounter: 0);
    }
}
