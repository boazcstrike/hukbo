using System.Linq;
using Sandata.Core.Maps;

namespace Sandata.Core.Tests;

/// <summary>
/// Covers the task-15 test bar: a scrambled line order canonicalises to a
/// byte-identical stream, a reversed endpoint is caught as a duplicate of
/// its normalised twin, a comment never moves the hash, and a single
/// coordinate change always does.
/// </summary>
public sealed class MapCanonicalizerTests
{
    private const string ValidHeader = "HKMAP 1\nNAME test-map\nGRID 640 720 4\n";

    private static string BuildMap(string body) => ValidHeader + body + "END\n";

    // ---- Endpoint normalisation ----

    [Fact]
    public void WallDrawnRightToLeftCanonicalisesIdenticallyToTheSameWallDrawnLeftToRight()
    {
        var leftToRight = MapTokenizer.Tokenize(BuildMap("WALL 0 0 640 0 1\n"));
        var rightToLeft = MapTokenizer.Tokenize(BuildMap("WALL 640 0 0 0 1\n"));

        var canonicalLeftToRight = MapCanonicalizer.Canonicalize(leftToRight);
        var canonicalRightToLeft = MapCanonicalizer.Canonicalize(rightToLeft);

        Assert.Equal(
            MapContentHash.Encode(canonicalLeftToRight).ToArray(),
            MapContentHash.Encode(canonicalRightToLeft).ToArray());
    }

    [Fact]
    public void DoorNormalisationSwapsEndpointsButLeavesHingeAndStateUntouched()
    {
        var records = MapTokenizer.Tokenize(BuildMap("DOOR 140 460 100 460 1 1\n"));

        var canonical = MapCanonicalizer.Canonicalize(records);
        var door = Assert.IsType<DoorRecord>(canonical.Single(r => r.Kind == MapRecordKind.Door));

        // Endpoints swapped into lexicographic ascending order...
        Assert.Equal(100, door.X1);
        Assert.Equal(460, door.Y1);
        Assert.Equal(140, door.X2);
        Assert.Equal(460, door.Y2);

        // ...but Hinge and State are carried through exactly as written. No
        // rule in design section 12 says a hinge should flip with the
        // endpoints it names, so normalisation leaves both alone.
        Assert.Equal(1, door.Hinge);
        Assert.Equal(1, door.State);
    }

    // ---- Duplicate detection ----

    [Fact]
    public void ReversedWallEndpointIsDetectedAsDuplicateOfItsNormalisedTwin()
    {
        var text = BuildMap("WALL 0 0 640 0 1\nWALL 640 0 0 0 1\n");
        var records = MapTokenizer.Tokenize(text);

        var ex = Assert.Throws<MapLoadException>(() => MapCanonicalizer.Canonicalize(records));

        Assert.Equal(MapCanonicalizer.DuplicateRecordRule, ex.Rule);
    }

    [Fact]
    public void SameEndpointsWithDifferentMaterialAreNotADuplicate()
    {
        var text = BuildMap("WALL 0 0 640 0 1\nWALL 0 0 640 0 2\n");
        var records = MapTokenizer.Tokenize(text);

        var canonical = MapCanonicalizer.Canonicalize(records);

        Assert.Equal(2, canonical.Count(r => r.Kind == MapRecordKind.Wall));
    }

    [Fact]
    public void DuplicateAcrossDifferentBodyKindsIsNeverFlagged()
    {
        // A WALL and a COVER can never tie under (kindOrdinal, fields...)
        // because their kind ordinals differ, regardless of field overlap.
        var records = MapTokenizer.Tokenize(BuildMap("WALL 0 0 640 0 1\nCOVER 0 0 640 100 0 32768 1\n"));

        var canonical = MapCanonicalizer.Canonicalize(records);

        Assert.Equal(1, canonical.Count(r => r.Kind == MapRecordKind.Wall));
        Assert.Equal(1, canonical.Count(r => r.Kind == MapRecordKind.Cover));
    }

    // ---- Sorting ----

    [Fact]
    public void BodyRecordsSortByKindOrdinalThenByFieldsAscending()
    {
        var text = BuildMap(
            "OBJECTIVE 0 100 100 10\n" +
            "WALL 100 0 0 0 1\n" +
            "WALL 0 0 50 0 1\n" +
            "SPAWN 0 10 10 0\n");

        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(text));
        var bodyKinds = canonical
            .Where(r => r.Kind is MapRecordKind.Wall or MapRecordKind.Spawn or MapRecordKind.Objective)
            .Select(r => r.Kind)
            .ToArray();

        Assert.Equal(
            [MapRecordKind.Wall, MapRecordKind.Wall, MapRecordKind.Spawn, MapRecordKind.Objective],
            bodyKinds);

        var walls = canonical.OfType<WallRecord>().ToArray();
        Assert.Equal(2, walls.Length);
        Assert.Equal(0, walls[0].X1);
        Assert.Equal(0, walls[1].X1);
        Assert.Equal(50, walls[0].X2);
        Assert.Equal(100, walls[1].X2);
    }

    [Fact]
    public void HeaderAndEndRecordsPassThroughUnchangedAndStayFirstAndLast()
    {
        var text = BuildMap("WALL 0 0 640 0 1\nWALL 0 640 640 640 1\n");

        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(text));

        Assert.Equal(MapRecordKind.Hkmap, canonical[0].Kind);
        Assert.Equal(MapRecordKind.Name, canonical[1].Kind);
        Assert.Equal(MapRecordKind.Grid, canonical[2].Kind);
        Assert.Equal(MapRecordKind.End, canonical[^1].Kind);
    }

    // ---- The task-15 test bar, verbatim ----

    [Fact]
    public void ScrambledLineOrderProducesAByteIdenticalCanonicalStreamToSortedInput()
    {
        const string sortedBody =
            "WALL 0 0 0 720 1\n" +
            "WALL 0 0 640 0 1\n" +
            "WALL 0 640 300 640 1\n" +
            "DOOR 100 460 140 460 0 0\n" +
            "DOOR 300 640 340 640 0 0\n" +
            "COVER 200 200 260 240 49152 8192 1\n" +
            "SPAWN 0 296 690 49152\n" +
            "SPAWN 1 120 520 49152\n" +
            "OBJECTIVE 0 500 120 48\n" +
            "OBJECTIVE 1 120 520 48\n";

        const string scrambledBody =
            "OBJECTIVE 1 120 520 48\n" +
            "SPAWN 1 120 520 49152\n" +
            "DOOR 300 640 340 640 0 0\n" +
            "WALL 0 640 300 640 1\n" +
            "OBJECTIVE 0 500 120 48\n" +
            "COVER 200 200 260 240 49152 8192 1\n" +
            "WALL 0 0 640 0 1\n" +
            "SPAWN 0 296 690 49152\n" +
            "DOOR 100 460 140 460 0 0\n" +
            "WALL 0 0 0 720 1\n";

        var sortedCanonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(BuildMap(sortedBody)));
        var scrambledCanonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(BuildMap(scrambledBody)));

        Assert.Equal(MapContentHash.Encode(sortedCanonical).ToArray(), MapContentHash.Encode(scrambledCanonical).ToArray());
        Assert.Equal(MapContentHash.Compute(sortedCanonical), MapContentHash.Compute(scrambledCanonical));
    }

    [Fact]
    public void AddingOrRemovingACommentDoesNotMoveTheHash()
    {
        const string bareBody = "WALL 0 0 640 0 1\nWALL 0 640 640 640 1\n";
        const string commentedBody =
            "# the north wall\n" +
            "WALL 0 0 640 0 1\n" +
            "\n" +
            "# the south wall, added later\n" +
            "WALL 0 640 640 640 1\n" +
            "\n";

        var bareHash = MapContentHash.Compute(
            MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(BuildMap(bareBody))));
        var commentedHash = MapContentHash.Compute(
            MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(BuildMap(commentedBody))));

        Assert.Equal(bareHash, commentedHash);
    }

    [Fact]
    public void ChangingOneCoordinateByOneMovesTheHash()
    {
        var original = MapContentHash.Compute(
            MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(BuildMap("WALL 0 0 640 0 1\n"))));
        var changed = MapContentHash.Compute(
            MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(BuildMap("WALL 1 0 640 0 1\n"))));

        Assert.NotEqual(original, changed);
    }

    // ---- Byte encoding, directly ----

    [Fact]
    public void EncodeProducesOneKindByteFollowedByFourBigEndianBytesPerField()
    {
        var records = MapTokenizer.Tokenize(BuildMap("WALL 1 2 3 4 1\n"));
        var canonical = MapCanonicalizer.Canonicalize(records);
        var wall = canonical.Single(r => r.Kind == MapRecordKind.Wall);

        var encoded = MapContentHash.Encode([wall]);

        byte[] expected =
        [
            (byte)MapRecordKind.Wall,
            0, 0, 0, 1, // X1
            0, 0, 0, 2, // Y1
            0, 0, 0, 3, // X2
            0, 0, 0, 4, // Y2
            0, 0, 0, 1, // Material
        ];
        Assert.Equal(expected, encoded.ToArray());
    }

    [Fact]
    public void NameRecordContributesOnlyItsKindByteToTheEncoding()
    {
        var records = MapTokenizer.Tokenize(BuildMap(""));
        var canonical = MapCanonicalizer.Canonicalize(records);
        var name = canonical.Single(r => r.Kind == MapRecordKind.Name);

        var encoded = MapContentHash.Encode([name]);

        Assert.Equal([(byte)MapRecordKind.Name], encoded.ToArray());
    }
}
