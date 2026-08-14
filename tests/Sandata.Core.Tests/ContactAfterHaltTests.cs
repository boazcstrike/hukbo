using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Sensing;

namespace Sandata.Core.Tests;

/// <summary>
/// Answers section 2.3 of the 2026-08-14 "the shipped mission freezes at first
/// contact" design, which asked why the surviving attacker never identifies a
/// hostile 88 world units away and offered two candidate explanations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both candidates were wrong and the sensing layer is behaving correctly.</b>
/// The survivor stops at <c>(412, 119)</c> and the defender stands at
/// <c>(500, 120)</c>, which is 88 world units apart and well inside
/// <see cref="ContactMemory.IdentifyRangeWu"/>. Between them, the map carries
/// <c>WALL 420 60 420 120</c>: a wall running down to exactly <c>y = 120</c>,
/// with the entrance to the objective room being the gap from there to
/// <c>WALL 420 160 420 200</c>. The survivor is standing one world unit north
/// of that opening, behind the wall. It cannot see the defender because there
/// is a wall in the way, and it is right not to.
/// </para>
/// <para>
/// Its dead squadmate fell at <c>(421, 120)</c> — one unit east of the same
/// wall line, in the aperture — which is exactly why that one was visible and
/// was shot. The two operators are nine world units apart and on opposite sides
/// of a wall.
/// </para>
/// <para>
/// <b>What this makes the real defect.</b> Nothing is wrong with contact
/// memory, the vision cone, or the identify range. What is wrong is that the
/// survivor stands behind that wall for the remaining 2,300 ticks of the run:
/// its group's path was consumed, its leader died, and no path is ever
/// re-requested, so it never steps through the opening its squadmate died in.
/// Section 2.3 of the design collapses into section 2.4, and the fix is the
/// re-plan decision D3 rather than anything in <c>src/Sandata.Core/Sensing</c>.
/// </para>
/// </remarks>
public sealed class ContactAfterHaltTests
{
    private const int SurvivorX = 412;
    private const int SurvivorY = 119;
    private const int DeadSquadmateX = 421;
    private const int DeadSquadmateY = 120;
    private const int DefenderX = 500;
    private const int DefenderY = 120;

    [Fact]
    public void TheSurvivorsRestingPositionHasNoLineOfSightToTheDefender()
    {
        var (grid, wallBuckets) = LoadAngleHouse();

        Assert.False(
            LineOfSight.IsVisible(SurvivorX, SurvivorY, DefenderX, DefenderY, grid, wallBuckets),
            "The survivor is behind the wall at x = 420, so it must not see the defender.");
    }

    [Fact]
    public void TheDeadSquadmatesPositionDoesHaveLineOfSightToTheDefender()
    {
        var (grid, wallBuckets) = LoadAngleHouse();

        Assert.True(
            LineOfSight.IsVisible(DeadSquadmateX, DeadSquadmateY, DefenderX, DefenderY, grid, wallBuckets),
            "The squadmate fell inside the aperture, which is why it was visible and was shot.");
    }

    /// <summary>
    /// The two positions are nine world units apart and both are far inside
    /// identify range, so range is not what separates them — the wall is. This
    /// is the assertion that stops a later reader from explaining the freeze as
    /// a range or a tier problem.
    /// </summary>
    [Fact]
    public void BothPositionsAreWellInsideIdentifyRangeOfTheDefender()
    {
        var survivorRangeSquared =
            ((long)DefenderX - SurvivorX) * ((long)DefenderX - SurvivorX) +
            ((long)DefenderY - SurvivorY) * ((long)DefenderY - SurvivorY);
        var squadmateRangeSquared =
            ((long)DefenderX - DeadSquadmateX) * ((long)DefenderX - DeadSquadmateX) +
            ((long)DefenderY - DeadSquadmateY) * ((long)DefenderY - DeadSquadmateY);
        var identifyRangeSquared = (long)ContactMemory.IdentifyRangeWu * ContactMemory.IdentifyRangeWu;

        Assert.True(survivorRangeSquared < identifyRangeSquared);
        Assert.True(squadmateRangeSquared < identifyRangeSquared);
    }

    /// <summary>
    /// The opening the survivor never walks through: the map's two wall records
    /// at <c>x = 420</c> stop at <c>y = 120</c> and resume at <c>y = 160</c>.
    /// Pinned so a later edit to the fixture that closes or moves that gap is
    /// caught here rather than by a mission that silently stops being winnable.
    /// </summary>
    [Fact]
    public void TheObjectiveRoomsApertureIsTheGapBetweenTheTwoWallsAtXFourTwenty()
    {
        var records = LoadRecords();
        var verticalWalls = records
            .OfType<WallRecord>()
            .Where(wall => wall.X1 == 420 && wall.X2 == 420)
            .OrderBy(wall => wall.Y1)
            .ToArray();

        Assert.Equal(2, verticalWalls.Length);
        Assert.Equal(120, verticalWalls[0].Y2);
        Assert.Equal(160, verticalWalls[1].Y1);
    }

    private static ImmutableArray<MapRecord> LoadRecords()
    {
        var path = Path.Combine(
            System.AppContext.BaseDirectory, "Fixtures", "angle-house.hkmap");
        var text = File.Exists(path)
            ? File.ReadAllText(path)
            : File.ReadAllText(Path.Combine("Fixtures", "angle-house.hkmap"));

        var records = MapTokenizer.Tokenize(text);
        MapValidator.Validate(records);
        return records;
    }

    private static (NavGrid Grid, WallBuckets WallBuckets) LoadAngleHouse()
    {
        var records = LoadRecords();
        var gridRecord = records.OfType<GridRecord>().Single();
        var walls = records.OfType<WallRecord>().ToImmutableArray();
        var doors = records.OfType<DoorRecord>().ToImmutableArray();

        var grid = new NavGrid(
            gridRecord.WidthWu / NavGrid.CellSizeWu,
            gridRecord.HeightWu / NavGrid.CellSizeWu);
        NavBake.Bake(grid, walls, doors, ShippedBodyRadiusWu);

        var wallBuckets = WallBuckets.Build(
            grid,
            walls.Select(wall => (long)wall.X1).ToArray(),
            walls.Select(wall => (long)wall.Y1).ToArray(),
            walls.Select(wall => (long)wall.X2).ToArray(),
            walls.Select(wall => (long)wall.Y2).ToArray());

        return (grid, wallBuckets);
    }

    /// <summary>
    /// The radius <c>SandataGame</c> bakes with, reproduced here because the
    /// client's constant is private to a type this project may not reference.
    /// </summary>
    private const int ShippedBodyRadiusWu = 5;
}
