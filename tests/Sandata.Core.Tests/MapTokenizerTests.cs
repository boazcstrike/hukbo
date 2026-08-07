using System.Linq;
using Sandata.Core.Maps;

namespace Sandata.Core.Tests;

/// <summary>
/// One test per named rejection rule in the task-8 test bar, plus a small
/// set of structural rules the tokenizer must also enforce to be a
/// working loader, plus a smoke test against the full worked example from
/// design section 12.
/// </summary>
public sealed class MapTokenizerTests
{
    private const string ValidHeader = "HKMAP 1\nNAME angle-house\nGRID 640 720 4\n";

    // ---- The fifteen named rejection rules ----

    [Fact]
    public void RejectsNegativeSign()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP -1\n"));

        Assert.Equal(MapLoadException.Rules.NegativeSign, ex.Rule);
        Assert.Equal(1, ex.LineNumber);
    }

    [Fact]
    public void RejectsDecimalPoint()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP 1.0\n"));

        Assert.Equal(MapLoadException.Rules.DecimalPoint, ex.Rule);
    }

    [Fact]
    public void RejectsGroupSeparator()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP 1,000\n"));

        Assert.Equal(MapLoadException.Rules.GroupSeparator, ex.Rule);
    }

    [Fact]
    public void RejectsLeadingWhitespace()
    {
        // A literal tab, not a space, so the single-space splitter leaves
        // it attached to the integer token instead of treating it as a
        // separator.
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP \t1\n"));

        Assert.Equal(MapLoadException.Rules.LeadingWhitespace, ex.Rule);
    }

    [Fact]
    public void RejectsTrailingWhitespace()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP 1\t\n"));

        Assert.Equal(MapLoadException.Rules.TrailingWhitespace, ex.Rule);
    }

    [Fact]
    public void RejectsEmptyToken()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP  1\n"));

        Assert.Equal(MapLoadException.Rules.EmptyToken, ex.Rule);
    }

    [Fact]
    public void RejectsUnknownRecordKind()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("FOO 1\n"));

        Assert.Equal(MapLoadException.Rules.UnknownRecordKind, ex.Rule);
    }

    [Fact]
    public void RejectsWrongTokenCount()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP 1 2\n"));

        Assert.Equal(MapLoadException.Rules.WrongTokenCount, ex.Rule);
    }

    [Fact]
    public void RejectsNonIntegerToken()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP abc\n"));

        Assert.Equal(MapLoadException.Rules.NonIntegerToken, ex.Rule);
    }

    [Fact]
    public void RejectsOutOfRangeField()
    {
        var ex = Assert.Throws<MapLoadException>(
            () => MapTokenizer.Tokenize("HKMAP 1\nNAME angle-house\nGRID 641 720 4\n"));

        Assert.Equal(MapLoadException.Rules.OutOfRangeField, ex.Rule);
    }

    [Fact]
    public void RejectsHkmapNotOnLineOne()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("# comment\nHKMAP 1\n"));

        Assert.Equal(MapLoadException.Rules.HkmapNotLineOne, ex.Rule);
        Assert.Equal(2, ex.LineNumber);
    }

    [Fact]
    public void RejectsWrongVersion()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize("HKMAP 2\n"));

        Assert.Equal(MapLoadException.Rules.WrongVersion, ex.Rule);
    }

    [Fact]
    public void RejectsEndNotLast()
    {
        var text = ValidHeader + "END\nWALL 0 0 640 0 1\n";

        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize(text));

        Assert.Equal(MapLoadException.Rules.EndNotLast, ex.Rule);
    }

    [Fact]
    public void RejectsGridCellSizeNotPowerOfTwo()
    {
        var ex = Assert.Throws<MapLoadException>(
            () => MapTokenizer.Tokenize("HKMAP 1\nNAME angle-house\nGRID 640 720 3\n"));

        Assert.Equal(MapLoadException.Rules.GridCellSizeNotPowerOfTwo, ex.Rule);
    }

    [Fact]
    public void RejectsGridDimensionOver512Cells()
    {
        // 513 cells at cell size 4 is 2052 world units.
        var ex = Assert.Throws<MapLoadException>(
            () => MapTokenizer.Tokenize("HKMAP 1\nNAME angle-house\nGRID 2052 4 4\n"));

        Assert.Equal(MapLoadException.Rules.GridDimensionOver512, ex.Rule);
    }

    // ---- Structural rules beyond the named-rule bar, still required by design section 12 ----

    [Fact]
    public void RejectsAMissingEndRecord()
    {
        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize(ValidHeader));

        Assert.Equal(MapLoadException.Rules.MissingEndRecord, ex.Rule);
    }

    [Fact]
    public void RejectsHeaderRecordsOutOfOrder()
    {
        var ex = Assert.Throws<MapLoadException>(
            () => MapTokenizer.Tokenize("HKMAP 1\nGRID 640 720 4\nNAME angle-house\n"));

        Assert.Equal(MapLoadException.Rules.HeaderOutOfOrder, ex.Rule);
    }

    [Fact]
    public void RejectsAnInvalidNameId()
    {
        var ex = Assert.Throws<MapLoadException>(
            () => MapTokenizer.Tokenize("HKMAP 1\nNAME Angle_House\nGRID 640 720 4\n"));

        Assert.Equal(MapLoadException.Rules.InvalidNameId, ex.Rule);
    }

    [Fact]
    public void RemovesBlankLinesAndCommentsBeforeParsing()
    {
        var text = "HKMAP 1\n\nNAME angle-house\n# a comment\nGRID 640 720 4\nEND\n";

        var records = MapTokenizer.Tokenize(text);

        Assert.Equal(4, records.Length);
        Assert.IsType<HkmapRecord>(records[0]);
        Assert.IsType<NameRecord>(records[1]);
        Assert.IsType<GridRecord>(records[2]);
        Assert.IsType<EndRecord>(records[3]);
    }

    [Fact]
    public void RejectsAWallWhoseEndpointIsOutsideTheMapBounds()
    {
        var text = ValidHeader + "WALL 0 0 641 0 1\nEND\n";

        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize(text));

        Assert.Equal(MapLoadException.Rules.OutOfRangeField, ex.Rule);
    }

    [Fact]
    public void RejectsADoorThatIsNotAxisAligned()
    {
        var text = ValidHeader + "DOOR 0 0 10 10 0 0\nEND\n";

        var ex = Assert.Throws<MapLoadException>(() => MapTokenizer.Tokenize(text));

        Assert.Equal(MapLoadException.Rules.OutOfRangeField, ex.Rule);
    }

    // ---- End-to-end smoke test against the worked example ----

    [Fact]
    public void ParsesTheAngleHouseWorkedExampleWithoutError()
    {
        const string angleHouse = """
            HKMAP 1
            NAME angle-house
            GRID 640 720 4
            WALL 0 0 0 720 1
            WALL 0 0 640 0 1
            WALL 0 640 300 640 1
            WALL 0 720 640 720 1
            WALL 60 260 200 340 2
            WALL 60 460 60 580 1
            WALL 60 460 100 460 1
            WALL 60 580 180 580 1
            WALL 120 120 320 220 2
            WALL 140 460 180 460 1
            WALL 160 400 340 520 2
            WALL 180 460 180 580 1
            WALL 320 220 520 160 2
            WALL 340 640 640 640 1
            WALL 380 380 560 300 2
            WALL 420 60 420 120 1
            WALL 420 60 600 60 1
            WALL 420 160 420 200 1
            WALL 420 200 600 200 3
            WALL 600 60 600 200 1
            WALL 640 0 640 720 1
            DOOR 100 460 140 460 0 0
            DOOR 300 640 340 640 0 0
            DOOR 420 120 420 160 1 1
            COVER 200 200 260 240 49152 8192 1
            COVER 260 100 340 140 16384 8192 2
            COVER 440 440 520 500 49152 8192 1
            COVER 500 540 560 600 0 32768 1
            SPAWN 0 296 690 49152
            SPAWN 0 320 690 49152
            SPAWN 1 120 520 49152
            SPAWN 1 500 120 16384
            OBJECTIVE 0 500 120 48
            OBJECTIVE 1 120 520 48
            END
            """;

        var records = MapTokenizer.Tokenize(angleHouse.ReplaceLineEndings("\n"));

        Assert.Equal(MapRecordKind.End, records[^1].Kind);
        Assert.Equal(21, records.Count(r => r.Kind == MapRecordKind.Wall));
        Assert.Equal(3, records.Count(r => r.Kind == MapRecordKind.Door));
        Assert.Equal(4, records.Count(r => r.Kind == MapRecordKind.Cover));
        Assert.Equal(4, records.Count(r => r.Kind == MapRecordKind.Spawn));
        Assert.Equal(2, records.Count(r => r.Kind == MapRecordKind.Objective));
    }
}
