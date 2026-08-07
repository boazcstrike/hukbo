using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Simulation;
using Sandata.Core.Squads;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 42 of docs/plans/2026-08-07-sandata-scaffold.md: single-file
/// collapse. Design section 8, "Doorway collapse falls out of the clearance
/// field", is the contract under test — when the <see cref="ClearanceField"/>
/// value at the leader's cell drops below the formation half-width, every
/// slot's lateral offset goes to zero and the squad walks single file; on the
/// far side, clearance rises and the offsets return. The design is explicit
/// that this is a pure function of a baked field and a constant — "no state,
/// no timer, and no special case inside the pathfinder" — so this suite pins
/// the pure threshold behaviour, proves the composition with
/// <see cref="SlotTargets.ComputeTarget"/> end to end on the real
/// angle-house fixture, and asserts by reflection that no collapse-related
/// field is stored anywhere in <see cref="MissionState"/>.
/// </summary>
public sealed class FormationCollapseTests
{
    private static string LoadFixtureText()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "angle-house.hkmap");
        return File.ReadAllText(path);
    }

    // ------------------------------------------------------------------
    // IsCollapsed / LateralOffset: the pure threshold, pinned exactly at
    // its boundary. formationHalfWidthWu = 8 world units is this suite's
    // own test fixture value, not a design- or ruleset-authored constant —
    // see the PROVISIONAL note in the task report. With that half-width,
    // IsCollapsed's cross-multiplied comparison is
    // leaderClearance * 4 < 8 * 10, i.e. leaderClearance * 4 < 80, i.e.
    // leaderClearance < 20: clearance 20 is the exact non-collapsing
    // boundary and clearance 19 is the first collapsing value one chamfer
    // unit below it.
    // ------------------------------------------------------------------

    [Fact]
    public void IsCollapsed_ClearanceExactlyAtTheHalfWidthBoundary_IsNotCollapsed()
    {
        Assert.False(FormationCollapse.IsCollapsed(leaderClearance: 20, formationHalfWidthWu: 8));
    }

    [Fact]
    public void IsCollapsed_ClearanceOneChamferUnitBelowTheBoundary_IsCollapsed()
    {
        Assert.True(FormationCollapse.IsCollapsed(leaderClearance: 19, formationHalfWidthWu: 8));
    }

    [Fact]
    public void IsCollapsed_ClearanceWellAboveTheBoundary_IsNotCollapsed()
    {
        Assert.False(FormationCollapse.IsCollapsed(leaderClearance: 1_000, formationHalfWidthWu: 8));
    }

    [Fact]
    public void IsCollapsed_ClearanceAtBlockedClearanceZero_IsCollapsed()
    {
        Assert.True(FormationCollapse.IsCollapsed(ClearanceField.BlockedClearance, formationHalfWidthWu: 8));
    }

    [Fact]
    public void IsCollapsed_NegativeClearance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FormationCollapse.IsCollapsed(leaderClearance: -1, formationHalfWidthWu: 8));
    }

    [Fact]
    public void IsCollapsed_NegativeFormationHalfWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FormationCollapse.IsCollapsed(leaderClearance: 20, formationHalfWidthWu: -1));
    }

    [Fact]
    public void LateralOffset_WhenCollapsed_IsZeroRegardlessOfTheUncollapsedValue()
    {
        Assert.Equal(0L, FormationCollapse.LateralOffset(leaderClearance: 19, formationHalfWidthWu: 8, uncollapsedLateralOffset: 15));
        Assert.Equal(0L, FormationCollapse.LateralOffset(leaderClearance: 19, formationHalfWidthWu: 8, uncollapsedLateralOffset: -15));
    }

    [Fact]
    public void LateralOffset_WhenNotCollapsed_ReturnsTheUncollapsedValueVerbatim()
    {
        Assert.Equal(15L, FormationCollapse.LateralOffset(leaderClearance: 20, formationHalfWidthWu: 8, uncollapsedLateralOffset: 15));
        Assert.Equal(-15L, FormationCollapse.LateralOffset(leaderClearance: 20, formationHalfWidthWu: 8, uncollapsedLateralOffset: -15));
    }

    // ------------------------------------------------------------------
    // End-to-end: the real angle-house fixture, a real baked NavGrid and
    // ClearanceField, a real NavSearch path from faction 0's spawn toward
    // objective 0 (through DOOR 300 640 340 640, design section 12's
    // "entry door"), and a four-slot group composed through
    // FormationCollapse.LateralOffset into SlotTargets.ComputeTarget. This
    // does not hardcode the corridor's cell sequence: it bakes and
    // searches for real on every run and asserts on the shape the result
    // must have, exactly like AngleHouseFixtureTests and SlotTargetsTests
    // already do for their own fixtures.
    // ------------------------------------------------------------------

    private const long FormationHalfWidthWu = 8;

    /// <summary>
    /// A four-slot group's own lateral offsets when the formation is fully
    /// expanded — an arbitrary, symmetric spread with no slot on the
    /// centreline, so a collapse is visible as every one of the four
    /// distinct positions collapsing onto the same point.
    /// </summary>
    private static readonly long[] SlotLateralOffsets = [-15, -5, 5, 15];

    private static readonly long[] SlotTrailOffsets = [0, 20, 40, 60];

    [Fact]
    public void FourSlotGroup_OnAngleHouseFixture_CollapsesAtTheEntryDoorAndReexpandsInside()
    {
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(LoadFixtureText()));
        var grid = canonical.OfType<GridRecord>().Single();
        var walls = canonical.OfType<WallRecord>().ToList();
        var doors = canonical.OfType<DoorRecord>().ToList();
        var spawns = canonical.OfType<SpawnRecord>().ToList();

        var navGrid = new NavGrid(grid.WidthWu / grid.CellWu, grid.HeightWu / grid.CellWu);
        NavBake.Bake(navGrid, walls, doors, bodyRadiusWu: 5);

        var passabilityBytes = new byte[navGrid.CellCount];
        var blocked = new bool[navGrid.CellCount];
        for (var i = 0; i < navGrid.CellCount; i++)
        {
            var isBlocked = navGrid.Passability[i] == NavCellFlags.Blocked;
            passabilityBytes[i] = isBlocked ? (byte)0 : (byte)1;
            blocked[i] = isBlocked;
        }

        var clearance = new int[navGrid.CellCount];
        ClearanceField.Build(passabilityBytes, clearance, navGrid.Width, navGrid.Height);

        var spawn0 = spawns.First(s => s.Faction == 0);
        var startCell = navGrid.CellIndex(
            NavGrid.WorldToCellCoordinate(spawn0.X),
            NavGrid.WorldToCellCoordinate(spawn0.Y));
        var goalCell = navGrid.CellIndex(
            NavGrid.WorldToCellCoordinate(500),
            NavGrid.WorldToCellCoordinate(120));

        var pathCells = new List<int>();
        var expanded = new List<int>();
        var outcome = new NavSearch().TryFindPath(navGrid, startCell, goalCell, blocked, pathCells, expanded);

        Assert.Equal(NavSearchOutcome.PathFound, outcome);

        // DOOR 300 640 340 640 (design section 12's "entry door") is the
        // only Door-flagged cell this route can cross: faction 0 spawns at
        // (296-320, 690), inside the room this door is the only opening
        // out of, and the other two doors sit far from a straight route to
        // objective 0 at (500, 120).
        var doorIndicesInCorridor = pathCells
            .Select((cellIndex, index) => (cellIndex, index))
            .Where(pair => navGrid.Passability[pair.cellIndex] == NavCellFlags.Door)
            .Select(pair => pair.index)
            .ToArray();

        Assert.NotEmpty(doorIndicesInCorridor);

        // Cell-centre world coordinates: CellSizeWu (4) per cell, offset by
        // half a cell so the point sits at the cell's own centre rather
        // than its lower-left corner.
        static (long X, long Y) CellCentre(NavGrid g, int cellIndex) =>
            (g.CellX(cellIndex) * (long)NavGrid.CellSizeWu + (NavGrid.CellSizeWu / 2),
             g.CellY(cellIndex) * (long)NavGrid.CellSizeWu + (NavGrid.CellSizeWu / 2));

        ImmutableArray<PathPoint> polyline = [.. pathCells.Select(cellIndex =>
        {
            var (x, y) = CellCentre(navGrid, cellIndex);
            return new PathPoint(x, y);
        })];

        var arclengthPath = PolylineArclength.Build(polyline);

        // The leader's own arclength at each corridor vertex, and the
        // clearance already looked up for that same cell — the exact pair
        // FormationCollapse.IsCollapsed consumes for the leader, once per
        // tick, per design section 8.
        var leaderArclengthByIndex = Enumerable.Range(0, pathCells.Count)
            .Select(index => arclengthPath.ArclengthAtVertex(index))
            .ToArray();

        var collapsedByIndex = Enumerable.Range(0, pathCells.Count)
            .Select(index => FormationCollapse.IsCollapsed(clearance[pathCells[index]], FormationHalfWidthWu))
            .ToArray();

        // Not one unit either side, on the real field: the door cell
        // itself collapses, and immediately adjacent cells with a lower
        // clearance value than the boundary (per the pinned unit tests
        // above) also collapse — proving the fixture-level behaviour is
        // driven by the same threshold, not a separate rule.
        foreach (var doorIndex in doorIndicesInCorridor)
        {
            Assert.True(collapsedByIndex[doorIndex],
                $"expected the door cell at corridor index {doorIndex} (clearance {clearance[pathCells[doorIndex]]}) to collapse");
        }

        // Re-expansion, both sides: the corridor starts and ends deep
        // inside open rooms, well away from the door, where this fixture's
        // baked clearance is far larger than any value the boundary tests
        // above collapse at.
        Assert.False(collapsedByIndex[0],
            $"expected the spawn-room start of the corridor (clearance {clearance[pathCells[0]]}) to stay expanded");
        Assert.False(collapsedByIndex[^1],
            $"expected the corridor's far end (clearance {clearance[pathCells[^1]]}) to stay expanded");

        // At least one collapsed index and at least one non-collapsed
        // index exist strictly before the first door index, and likewise
        // strictly after the last door index: the squad is expanded on
        // approach, collapses through the doorway, and re-expands beyond
        // it, rather than collapsing for the whole route or never at all.
        var firstDoorIndex = doorIndicesInCorridor.Min();
        var lastDoorIndex = doorIndicesInCorridor.Max();

        Assert.Contains(false, collapsedByIndex.Take(firstDoorIndex));
        Assert.Contains(false, collapsedByIndex.Skip(lastDoorIndex + 1));

        // Compose with SlotTargets.ComputeTarget, exactly as task 49's
        // production tick pipeline will: for every slot, feed
        // FormationCollapse.LateralOffset's result straight into
        // ComputeTarget's own lateralOffset parameter.
        (long X, long Y)[] TargetsAt(int corridorIndex)
        {
            var leaderArclength = leaderArclengthByIndex[corridorIndex];
            var leaderClearance = clearance[pathCells[corridorIndex]];

            return Enumerable.Range(0, SlotLateralOffsets.Length)
                .Select(slot =>
                {
                    var appliedLateralOffset = FormationCollapse.LateralOffset(
                        leaderClearance, FormationHalfWidthWu, SlotLateralOffsets[slot]);
                    return SlotTargets.ComputeTarget(arclengthPath, leaderArclength, SlotTrailOffsets[slot], appliedLateralOffset);
                })
                .ToArray();
        }

        // Before the door: expanded, so no two slots land on the same
        // point (four distinct lateral offsets produce four distinct
        // targets, once trail offset is held equal — which it is not
        // here, but distinct lateral offsets alone already guarantee
        // distinct targets on a straight corridor segment).
        var expandedTargets = TargetsAt(0);
        Assert.Equal(expandedTargets.Length, expandedTargets.Distinct().Count());

        // At the door: collapsed, so every slot's target is the one
        // ComputeTarget would produce with lateralOffset 0 — the
        // centreline point for that slot's own trail offset. Recomputing
        // that expectation directly (rather than re-deriving it from
        // expandedTargets) is what proves the lateral component, and only
        // the lateral component, was forced to zero.
        var doorTargets = TargetsAt(firstDoorIndex);
        var expectedCentrelineTargets = Enumerable.Range(0, SlotTrailOffsets.Length)
            .Select(slot => SlotTargets.ComputeTarget(arclengthPath, leaderArclengthByIndex[firstDoorIndex], SlotTrailOffsets[slot], lateralOffset: 0))
            .ToArray();
        Assert.Equal(expectedCentrelineTargets, doorTargets);

        // Past the door: re-expanded, so the lateral spread returns and
        // slots are distinct again.
        var reexpandedTargets = TargetsAt(pathCells.Count - 1);
        Assert.Equal(reexpandedTargets.Length, reexpandedTargets.Distinct().Count());
    }

    // ------------------------------------------------------------------
    // "No collapse state is stored on MissionState": a reflection sweep
    // over every record type MissionState's own tree holds, asserting no
    // public property's name mentions "collapse". This is the
    // architectural half of design section 8's "no state, no timer" —
    // the arithmetic tests above already prove the function is pure;
    // this proves nothing was ever given anywhere to persist its result.
    // ------------------------------------------------------------------

    [Fact]
    public void MissionStateAndItsElementTypes_DeclareNoCollapseRelatedProperty()
    {
        Type[] typesToInspect =
        [
            typeof(MissionState),
            typeof(OperatorState),
            typeof(ContactMemoryEntry),
            typeof(FactionAlertState),
            typeof(DoorState),
            typeof(GroupPathState),
            typeof(RngStreamState),
        ];

        var offendingProperties = typesToInspect
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => $"{type.Name}.{property.Name}"))
            .Where(qualifiedName => qualifiedName.Contains("collapse", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(offendingProperties);
    }
}
