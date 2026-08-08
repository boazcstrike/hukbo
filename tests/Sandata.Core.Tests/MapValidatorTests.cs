using System.Collections.Generic;
using System.Linq;
using Sandata.Core.Maps;

namespace Sandata.Core.Tests;

/// <summary>
/// One failing-fixture test per <see cref="MapValidator.Rules"/> entry, plus
/// the two design-decision fixtures <see cref="MapValidator"/>'s class doc
/// comment names: whether a map's <c>NAME</c> reaches
/// <see cref="MapContentHash"/>, and whether a <c>DOOR</c>'s
/// <see cref="DoorRecord.Hinge"/> is absolute or relative to authored
/// endpoint order. Every failing scenario is built by mutating exactly one
/// field of <see cref="WellFormedMap"/>, a small closed box that otherwise
/// passes every rule, so each test isolates one violation.
/// </summary>
public sealed class MapValidatorTests
{
    private const string SmallBoxHeader = "HKMAP 1\nNAME test-box\nGRID 40 40 4\n";
    private const string LargeGridHeader = "HKMAP 1\nNAME test-door\nGRID 640 720 4\n";

    /// <summary>
    /// A 40x40 wu closed box (10x10 cells at cellWu 4), no interior walls,
    /// no door, one spawn per faction well clear of the outer shell and of
    /// each other, one objective reachable from faction 0. Every rule in
    /// <see cref="MapValidator.Rules"/> passes against this map unmodified.
    /// </summary>
    private const string WellFormedMap =
        SmallBoxHeader +
        "WALL 0 0 0 40 1\n" +
        "WALL 0 0 40 0 1\n" +
        "WALL 40 0 40 40 1\n" +
        "WALL 0 40 40 40 1\n" +
        "SPAWN 0 8 8 0\n" +
        "SPAWN 1 32 32 0\n" +
        "OBJECTIVE 0 20 20 4\n" +
        "END\n";

    private static IReadOnlyList<MapRecord> Load(string text) =>
        MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(text));

    [Fact]
    public void WellFormedMapPassesEveryCrossRecordRuleWithoutThrowing()
    {
        MapValidator.Validate(Load(WellFormedMap));
    }

    // ---- Spawn presence ----

    [Fact]
    public void MapWithNoFaction0SpawnFailsSpawnPresence()
    {
        var text = SmallBoxHeader +
            "WALL 0 0 0 40 1\nWALL 0 0 40 0 1\nWALL 40 0 40 40 1\nWALL 0 40 40 40 1\n" +
            "SPAWN 1 32 32 0\n" +
            "OBJECTIVE 0 20 20 4\n" +
            "END\n";

        var ex = Assert.Throws<MapLoadException>(() => MapValidator.Validate(Load(text)));

        Assert.Equal(MapValidator.Rules.MissingFaction0Spawn, ex.Rule);
    }

    [Fact]
    public void MapWithNoFaction1SpawnFailsSpawnPresence()
    {
        var text = SmallBoxHeader +
            "WALL 0 0 0 40 1\nWALL 0 0 40 0 1\nWALL 40 0 40 40 1\nWALL 0 40 40 40 1\n" +
            "SPAWN 0 8 8 0\n" +
            "OBJECTIVE 0 20 20 4\n" +
            "END\n";

        var ex = Assert.Throws<MapLoadException>(() => MapValidator.Validate(Load(text)));

        Assert.Equal(MapValidator.Rules.MissingFaction1Spawn, ex.Rule);
    }

    // ---- Spawn separation ----

    [Fact]
    public void TwoSpawnsCloserThanOneBodyDiameterFailSeparation()
    {
        var text = SmallBoxHeader +
            "WALL 0 0 0 40 1\nWALL 0 0 40 0 1\nWALL 40 0 40 40 1\nWALL 0 40 40 40 1\n" +
            "SPAWN 0 8 8 0\n" +
            "SPAWN 1 9 8 0\n" +
            "OBJECTIVE 0 20 20 4\n" +
            "END\n";

        var ex = Assert.Throws<MapLoadException>(() => MapValidator.Validate(Load(text)));

        Assert.Equal(MapValidator.Rules.SpawnsTooClose, ex.Rule);
    }

    // ---- Enclosure ----

    [Fact]
    public void MapWithAGapInTheOuterShellFailsEnclosure()
    {
        // North wall removed: the flood fill seeded from outside the
        // bounding box now walks straight in through row 0.
        var text = SmallBoxHeader +
            "WALL 0 0 0 40 1\nWALL 40 0 40 40 1\nWALL 0 40 40 40 1\n" +
            "SPAWN 0 8 8 0\n" +
            "SPAWN 1 32 32 0\n" +
            "OBJECTIVE 0 20 20 4\n" +
            "END\n";

        var ex = Assert.Throws<MapLoadException>(() => MapValidator.Validate(Load(text)));

        Assert.Equal(MapValidator.Rules.MapNotFullyEnclosed, ex.Rule);
    }

    // ---- Objective index density (this task's "no duplicate of any kind") ----

    /// <summary>
    /// Two <see cref="ObjectiveRecord"/>s sharing index 0 at two different
    /// coordinates. <see cref="MapCanonicalizer.Canonicalize"/>'s
    /// field-equality duplicate check waves this through — the fields
    /// genuinely differ — which is exactly why this rule exists as
    /// <see cref="MapValidator"/>'s own, broader reading of "no duplicate
    /// record of any kind".
    /// </summary>
    [Fact]
    public void TwoObjectivesSharingAnIndexFailObjectiveIndexDensity()
    {
        var text = SmallBoxHeader +
            "WALL 0 0 0 40 1\nWALL 0 0 40 0 1\nWALL 40 0 40 40 1\nWALL 0 40 40 40 1\n" +
            "SPAWN 0 8 8 0\n" +
            "SPAWN 1 32 32 0\n" +
            "OBJECTIVE 0 20 20 4\n" +
            "OBJECTIVE 0 12 12 4\n" +
            "END\n";

        var ex = Assert.Throws<MapLoadException>(() => MapValidator.Validate(Load(text)));

        Assert.Equal(MapValidator.Rules.ObjectiveIndicesNotDenseFromZero, ex.Rule);
    }

    [Fact]
    public void ObjectiveIndicesWithAGapFailObjectiveIndexDensity()
    {
        var text = SmallBoxHeader +
            "WALL 0 0 0 40 1\nWALL 0 0 40 0 1\nWALL 40 0 40 40 1\nWALL 0 40 40 40 1\n" +
            "SPAWN 0 8 8 0\n" +
            "SPAWN 1 32 32 0\n" +
            "OBJECTIVE 1 20 20 4\n" +
            "END\n";

        var ex = Assert.Throws<MapLoadException>(() => MapValidator.Validate(Load(text)));

        Assert.Equal(MapValidator.Rules.ObjectiveIndicesNotDenseFromZero, ex.Rule);
    }

    // ---- Reachability ----

    [Fact]
    public void ObjectiveSealedBehindDoorlessWallsFailsReachability()
    {
        // A complete, doorless 8x8 wu room around the objective, sharing
        // no wall with the outer shell. Fully enclosed on its own (so
        // enclosure still passes for every spawn), but nothing but the
        // objective is inside it, so faction 0 can never walk in.
        var text = SmallBoxHeader +
            "WALL 0 0 0 40 1\nWALL 0 0 40 0 1\nWALL 40 0 40 40 1\nWALL 0 40 40 40 1\n" +
            "WALL 16 16 16 24 1\nWALL 16 16 24 16 1\nWALL 24 16 24 24 1\nWALL 16 24 24 24 1\n" +
            "SPAWN 0 8 8 0\n" +
            "SPAWN 1 32 32 0\n" +
            "OBJECTIVE 0 20 20 4\n" +
            "END\n";

        var ex = Assert.Throws<MapLoadException>(() => MapValidator.Validate(Load(text)));

        Assert.Equal(MapValidator.Rules.ObjectiveUnreachableFromFaction0, ex.Rule);
    }

    /// <summary>
    /// A closed door (State 0) sits in the south wall of a 16x16 wu room
    /// (x 32-48, y 32-48) that otherwise seals the objective off exactly as
    /// <see cref="ObjectiveSealedBehindDoorlessWallsFailsReachability"/>
    /// does. Reachability must treat every door as passable regardless of
    /// its authored State, per design section 12's own wording for this
    /// rule, so this map passes where the doorless version fails.
    /// </summary>
    /// <remarks>
    /// Uses an 80x80 wu grid, not the 40x40 <see cref="SmallBoxHeader"/> box:
    /// rasterising a wall into cells marks every cell its bounding box
    /// touches, including one it only meets at a shared corner point with an
    /// adjoining wall or door — the south wall segment (x 32-40) touches the
    /// door's first cell (x 40-44) at their shared corner (40, 48), and the
    /// east wall touches the door's last cell (x 44-48) at (48, 48) the same
    /// way, leaving only the door's middle cell (x 44-48... the second of
    /// its three) genuinely clear. On the 40x40 box that one clear cell sat
    /// directly against the outer shell's own far-boundary row, which the
    /// clamp in <c>MarkBlockedCells</c> marks fully blocked, isolating it
    /// with no approach from outside; the larger grid leaves an untouched
    /// row between the room and the outer shell for the flood fill to
    /// approach that cell from.
    /// </remarks>
    [Fact]
    public void ClosedDoorInTheSealedRoomStillPassesReachability()
    {
        const string bigBoxHeader = "HKMAP 1\nNAME test-box-with-door\nGRID 80 80 4\n";
        var text = bigBoxHeader +
            "WALL 0 0 0 80 1\nWALL 0 0 80 0 1\nWALL 80 0 80 80 1\nWALL 0 80 80 80 1\n" +
            "WALL 32 32 32 48 1\nWALL 32 32 48 32 1\nWALL 48 32 48 48 1\n" +
            "WALL 32 48 40 48 1\nDOOR 40 48 48 48 0 0\n" +
            "SPAWN 0 8 8 0\n" +
            "SPAWN 1 72 72 0\n" +
            "OBJECTIVE 0 40 40 4\n" +
            "END\n";

        MapValidator.Validate(Load(text));
    }

    // ---- Decision 1: does a map's NAME reach the content hash? ----

    /// <summary>
    /// Pins <see cref="MapValidator"/>'s decision 1: a map's <c>NAME</c>
    /// never reaches <see cref="MapContentHash"/>. Two maps identical in
    /// every field except <c>NAME</c> must hash identically — a rename is
    /// not a content-affecting edit and must not force a new golden replay
    /// expectation.
    /// </summary>
    [Fact]
    public void MapNameDoesNotReachTheContentHash_TwoMapsDifferingOnlyByNameHashIdentically()
    {
        const string body = "GRID 640 720 4\nWALL 0 0 640 0 1\nEND\n";
        var first = Load("HKMAP 1\nNAME first-map\n" + body);
        var second = Load("HKMAP 1\nNAME second-map\n" + body);

        Assert.Equal(MapContentHash.Compute(first), MapContentHash.Compute(second));
    }

    // ---- Decision 2: does canonicalising a door move its hinge? ----

    /// <summary>
    /// Pins <see cref="MapValidator"/>'s decision 2, and is the fixture
    /// that would fail under the rejected relative reading: a door
    /// physically hinged at world point (140, 460), authored twice with its
    /// endpoints in opposite order, hinge written per the absolute rule
    /// both times — "1" names whichever endpoint sorts larger, so both
    /// authorings use hinge 1 for the same physical hinge point. The two
    /// canonicalise byte-identically today, because
    /// <see cref="MapCanonicalizer.Canonicalize"/> never touches
    /// <see cref="DoorRecord.Hinge"/> on an endpoint swap. If that method
    /// were ever changed to flip Hinge on a swap — the fix a relative
    /// reading would call for — the descending authoring's canonical Hinge
    /// would disagree with the ascending authoring's, and this assertion
    /// would fail.
    /// </summary>
    [Fact]
    public void DoorHingeIsAbsoluteToCanonicalEndpointOrder_NotRelativeToAuthoredOrder()
    {
        var ascending = Load(LargeGridHeader + "DOOR 100 460 140 460 1 0\nEND\n");
        var descending = Load(LargeGridHeader + "DOOR 140 460 100 460 1 0\nEND\n");

        Assert.Equal(
            MapContentHash.Encode(ascending).ToArray(),
            MapContentHash.Encode(descending).ToArray());
    }
}
