using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Sandata.Core.Maps;

/// <summary>
/// Converts the raw text of an `.hkmap` file (design section 12) into a
/// sequence of per-record-validated <see cref="MapRecord"/> values. Takes
/// text the caller has already read; <c>Sandata.Core</c> is forbidden the
/// filesystem, so this type never opens a file itself.
///
/// Blank lines and lines whose first character is <c>#</c> are removed
/// before parsing and do not count as records for the header-order rules
/// below. Tokens are separated by exactly one literal space character; an
/// embedded tab or other whitespace character is deliberately left
/// attached to its neighbouring token rather than treated as a separator,
/// so that <see cref="int.Parse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?)"/>
/// with <see cref="NumberStyles.None"/> can reject it as leading or
/// trailing whitespace on the integer itself.
///
/// Every malformed input — an unknown record kind, a wrong token count, a
/// non-integer token, an out-of-range field, a header out of order, or a
/// misplaced <c>END</c> — is a hard <see cref="MapLoadException"/> naming
/// the line number of the original text and the rule broken. No line is
/// ever skipped and no field is ever defaulted.
///
/// Cross-record validation — spawn presence and separation, full
/// enclosure, faction-0 reachability to every objective, and duplicate
/// detection of any kind — is <c>MapValidator</c>'s job (task 25), not
/// this type's.
/// </summary>
public static class MapTokenizer
{
    private const int MaxGridCellsPerAxis = 512;
    private const int MaxBam16Value = 65_535;

    /// <summary>
    /// Parses <paramref name="mapText"/> into its per-record-validated
    /// records, in file order. Throws <see cref="MapLoadException"/> on
    /// the first rule violation encountered.
    /// </summary>
    public static ImmutableArray<MapRecord> Tokenize(string mapText)
    {
        ArgumentNullException.ThrowIfNull(mapText);

        var records = ImmutableArray.CreateBuilder<MapRecord>();
        var stage = HeaderStage.ExpectHkmap;
        GridRecord? grid = null;
        var sawEnd = false;
        var lineNumber = 0;

        foreach (var lineSpan in mapText.AsSpan().EnumerateLines())
        {
            lineNumber++;

            if (lineSpan.IsEmpty || lineSpan[0] == '#')
            {
                // Blank and comment lines are removed before parsing and
                // never reach the header-order or END-is-last rules.
                continue;
            }

            if (sawEnd)
            {
                throw new MapLoadException(
                    lineNumber,
                    MapLoadException.Rules.EndNotLast,
                    $"line {lineNumber}: END must be the last record in the file, but another record follows it.");
            }

            var tokens = lineSpan.ToString().Split(' ');
            for (var i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Length == 0)
                {
                    throw new MapLoadException(
                        lineNumber,
                        MapLoadException.Rules.EmptyToken,
                        $"line {lineNumber}: token {i + 1} is empty. Tokens are separated by exactly one space.");
                }
            }

            var kindToken = tokens[0];
            if (!TryParseKind(kindToken, out var kind))
            {
                throw new MapLoadException(
                    lineNumber,
                    MapLoadException.Rules.UnknownRecordKind,
                    $"line {lineNumber}: '{kindToken}' is not one of the nine known record kinds.");
            }

            switch (stage)
            {
                case HeaderStage.ExpectHkmap:
                    if (kind != MapRecordKind.Hkmap || lineNumber != 1)
                    {
                        throw new MapLoadException(
                            lineNumber,
                            MapLoadException.Rules.HkmapNotLineOne,
                            $"line {lineNumber}: HKMAP must be the first record in the file, on line 1.");
                    }

                    records.Add(ParseHkmap(tokens, lineNumber));
                    stage = HeaderStage.ExpectName;
                    break;

                case HeaderStage.ExpectName:
                    if (kind != MapRecordKind.Name)
                    {
                        throw new MapLoadException(
                            lineNumber,
                            MapLoadException.Rules.HeaderOutOfOrder,
                            $"line {lineNumber}: expected NAME immediately after HKMAP, found {kind}.");
                    }

                    records.Add(ParseName(tokens, lineNumber));
                    stage = HeaderStage.ExpectGrid;
                    break;

                case HeaderStage.ExpectGrid:
                    if (kind != MapRecordKind.Grid)
                    {
                        throw new MapLoadException(
                            lineNumber,
                            MapLoadException.Rules.HeaderOutOfOrder,
                            $"line {lineNumber}: expected GRID immediately after NAME, found {kind}.");
                    }

                    grid = ParseGrid(tokens, lineNumber);
                    records.Add(grid);
                    stage = HeaderStage.Body;
                    break;

                case HeaderStage.Body:
                    if (grid is null)
                    {
                        throw new InvalidOperationException("Body stage reached without a GRID record having been parsed first.");
                    }

                    records.Add(ParseBodyRecord(kind, tokens, lineNumber, grid));
                    if (kind == MapRecordKind.End)
                    {
                        sawEnd = true;
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unreachable header stage {stage}.");
            }
        }

        if (!sawEnd)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.MissingEndRecord,
                "The file must end with an END record, and none was found.");
        }

        return records.ToImmutable();
    }

    private static MapRecord ParseBodyRecord(MapRecordKind kind, string[] tokens, int lineNumber, GridRecord grid) =>
        kind switch
        {
            MapRecordKind.Wall => ParseWall(tokens, lineNumber, grid),
            MapRecordKind.Door => ParseDoor(tokens, lineNumber, grid),
            MapRecordKind.Cover => ParseCover(tokens, lineNumber, grid),
            MapRecordKind.Spawn => ParseSpawn(tokens, lineNumber, grid),
            MapRecordKind.Objective => ParseObjective(tokens, lineNumber, grid),
            MapRecordKind.End => ParseEnd(tokens, lineNumber),
            _ => throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.HeaderOutOfOrder,
                $"line {lineNumber}: {kind} may not appear after the header."),
        };

    private static HkmapRecord ParseHkmap(string[] tokens, int lineNumber)
    {
        RequireTokenCount(tokens, 2, lineNumber);
        var version = ParseIntToken(tokens[1], lineNumber);
        if (version != 1)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.WrongVersion,
                $"line {lineNumber}: HKMAP version must be 1, was {version}.");
        }

        return new HkmapRecord(lineNumber, version);
    }

    private static NameRecord ParseName(string[] tokens, int lineNumber)
    {
        RequireTokenCount(tokens, 2, lineNumber);
        var id = tokens[1];
        if (id.Length is < 1 or > 32 || !IsValidNameId(id))
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.InvalidNameId,
                $"line {lineNumber}: NAME id '{id}' must match [a-z0-9-]{{1,32}}.");
        }

        return new NameRecord(lineNumber, id);
    }

    private static GridRecord ParseGrid(string[] tokens, int lineNumber)
    {
        RequireTokenCount(tokens, 4, lineNumber);
        var widthWu = ParseIntToken(tokens[1], lineNumber);
        var heightWu = ParseIntToken(tokens[2], lineNumber);
        var cellWu = ParseIntToken(tokens[3], lineNumber);

        if (!IsPowerOfTwo(cellWu))
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.GridCellSizeNotPowerOfTwo,
                $"line {lineNumber}: GRID cellWu {cellWu} must be a power of two.");
        }

        if (widthWu <= 0 || widthWu % cellWu != 0)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: GRID widthWu {widthWu} must be a positive multiple of cellWu {cellWu}.");
        }

        if (heightWu <= 0 || heightWu % cellWu != 0)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: GRID heightWu {heightWu} must be a positive multiple of cellWu {cellWu}.");
        }

        if (widthWu / cellWu > MaxGridCellsPerAxis || heightWu / cellWu > MaxGridCellsPerAxis)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.GridDimensionOver512,
                $"line {lineNumber}: GRID dimensions must span at most {MaxGridCellsPerAxis} cells per axis.");
        }

        return new GridRecord(lineNumber, widthWu, heightWu, cellWu);
    }

    private static WallRecord ParseWall(string[] tokens, int lineNumber, GridRecord grid)
    {
        RequireTokenCount(tokens, 6, lineNumber);
        var x1 = ParseIntToken(tokens[1], lineNumber);
        var y1 = ParseIntToken(tokens[2], lineNumber);
        var x2 = ParseIntToken(tokens[3], lineNumber);
        var y2 = ParseIntToken(tokens[4], lineNumber);
        var material = ParseIntToken(tokens[5], lineNumber);

        RequireInBounds(x1, y1, grid, lineNumber);
        RequireInBounds(x2, y2, grid, lineNumber);

        if (x1 == x2 && y1 == y2)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: WALL endpoints ({x1}, {y1}) and ({x2}, {y2}) must differ.");
        }

        if (material is < 0 or > 3)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: WALL material {material} must be 0..3.");
        }

        return new WallRecord(lineNumber, x1, y1, x2, y2, material);
    }

    private static DoorRecord ParseDoor(string[] tokens, int lineNumber, GridRecord grid)
    {
        RequireTokenCount(tokens, 7, lineNumber);
        var x1 = ParseIntToken(tokens[1], lineNumber);
        var y1 = ParseIntToken(tokens[2], lineNumber);
        var x2 = ParseIntToken(tokens[3], lineNumber);
        var y2 = ParseIntToken(tokens[4], lineNumber);
        var hinge = ParseIntToken(tokens[5], lineNumber);
        var state = ParseIntToken(tokens[6], lineNumber);

        RequireInBounds(x1, y1, grid, lineNumber);
        RequireInBounds(x2, y2, grid, lineNumber);

        if (x1 == x2 && y1 == y2)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: DOOR endpoints ({x1}, {y1}) and ({x2}, {y2}) must differ.");
        }

        if (x1 != x2 && y1 != y2)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: DOOR endpoints ({x1}, {y1}) and ({x2}, {y2}) must be axis-aligned.");
        }

        if (hinge is < 0 or > 1)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: DOOR hinge {hinge} must be 0..1.");
        }

        if (state is < 0 or > 1)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: DOOR state {state} must be 0..1.");
        }

        return new DoorRecord(lineNumber, x1, y1, x2, y2, hinge, state);
    }

    private static CoverRecord ParseCover(string[] tokens, int lineNumber, GridRecord grid)
    {
        RequireTokenCount(tokens, 8, lineNumber);
        var minX = ParseIntToken(tokens[1], lineNumber);
        var minY = ParseIntToken(tokens[2], lineNumber);
        var maxX = ParseIntToken(tokens[3], lineNumber);
        var maxY = ParseIntToken(tokens[4], lineNumber);
        var arcCentreBam = ParseIntToken(tokens[5], lineNumber);
        var arcHalfBam = ParseIntToken(tokens[6], lineNumber);
        var height = ParseIntToken(tokens[7], lineNumber);

        RequireInBounds(minX, minY, grid, lineNumber);
        RequireInBounds(maxX, maxY, grid, lineNumber);

        if (minX >= maxX)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: COVER minX {minX} must be strictly less than maxX {maxX}.");
        }

        if (minY >= maxY)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: COVER minY {minY} must be strictly less than maxY {maxY}.");
        }

        if (arcCentreBam < 0 || arcCentreBam > MaxBam16Value)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: COVER arcCentreBam {arcCentreBam} must be 0..{MaxBam16Value}.");
        }

        if (arcHalfBam is < 1 or > 32_768)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: COVER arcHalfBam {arcHalfBam} must be 1..32768.");
        }

        if (height is < 0 or > 2)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: COVER height {height} must be 0..2.");
        }

        return new CoverRecord(lineNumber, minX, minY, maxX, maxY, arcCentreBam, arcHalfBam, height);
    }

    private static SpawnRecord ParseSpawn(string[] tokens, int lineNumber, GridRecord grid)
    {
        RequireTokenCount(tokens, 5, lineNumber);
        var faction = ParseIntToken(tokens[1], lineNumber);
        var x = ParseIntToken(tokens[2], lineNumber);
        var y = ParseIntToken(tokens[3], lineNumber);
        var facingBam = ParseIntToken(tokens[4], lineNumber);

        if (faction is < 0 or > 1)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: SPAWN faction {faction} must be 0..1.");
        }

        RequireInBounds(x, y, grid, lineNumber);

        if (facingBam < 0 || facingBam > MaxBam16Value)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: SPAWN facingBam {facingBam} must be 0..{MaxBam16Value}.");
        }

        return new SpawnRecord(lineNumber, faction, x, y, facingBam);
    }

    private static ObjectiveRecord ParseObjective(string[] tokens, int lineNumber, GridRecord grid)
    {
        RequireTokenCount(tokens, 5, lineNumber);
        var index = ParseIntToken(tokens[1], lineNumber);
        var x = ParseIntToken(tokens[2], lineNumber);
        var y = ParseIntToken(tokens[3], lineNumber);
        var radiusWu = ParseIntToken(tokens[4], lineNumber);

        if (index < 0)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: OBJECTIVE index {index} must be non-negative.");
        }

        RequireInBounds(x, y, grid, lineNumber);

        if (radiusWu <= 0)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: OBJECTIVE radiusWu {radiusWu} must be positive.");
        }

        return new ObjectiveRecord(lineNumber, index, x, y, radiusWu);
    }

    private static EndRecord ParseEnd(string[] tokens, int lineNumber)
    {
        RequireTokenCount(tokens, 1, lineNumber);
        return new EndRecord(lineNumber);
    }

    private static void RequireInBounds(int x, int y, GridRecord grid, int lineNumber)
    {
        if (x < 0 || x > grid.WidthWu || y < 0 || y > grid.HeightWu)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.OutOfRangeField,
                $"line {lineNumber}: point ({x}, {y}) is outside the map bounds [0, {grid.WidthWu}] x [0, {grid.HeightWu}].");
        }
    }

    private static void RequireTokenCount(string[] tokens, int expected, int lineNumber)
    {
        if (tokens.Length != expected)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.WrongTokenCount,
                $"line {lineNumber}: expected {expected} tokens, found {tokens.Length}.");
        }
    }

    /// <summary>
    /// Parses one integer field. Rejects, in order, an empty token
    /// (defensive; every token has already been checked non-empty by the
    /// caller), a leading '-', an embedded '.', an embedded ',', a leading
    /// whitespace character, and a trailing whitespace character, before
    /// falling through to <see cref="int.Parse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?)"/>
    /// with <see cref="NumberStyles.None"/> and
    /// <see cref="CultureInfo.InvariantCulture"/> for everything else a
    /// culture-independent integer cannot be.
    /// </summary>
    private static int ParseIntToken(string token, int lineNumber)
    {
        if (token.Length == 0)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.EmptyToken,
                $"line {lineNumber}: an integer field was empty.");
        }

        if (token[0] == '-')
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.NegativeSign,
                $"line {lineNumber}: token '{token}' has a leading '-'. The format cannot express a negative number.");
        }

        if (token.Contains('.'))
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.DecimalPoint,
                $"line {lineNumber}: token '{token}' has a decimal point. The format has no syntax for a fraction.");
        }

        if (token.Contains(','))
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.GroupSeparator,
                $"line {lineNumber}: token '{token}' has a group separator.");
        }

        if (char.IsWhiteSpace(token[0]))
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.LeadingWhitespace,
                $"line {lineNumber}: token '{token}' has leading whitespace.");
        }

        if (char.IsWhiteSpace(token[^1]))
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.TrailingWhitespace,
                $"line {lineNumber}: token '{token}' has trailing whitespace.");
        }

        try
        {
            return int.Parse(token.AsSpan(), NumberStyles.None, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.NonIntegerToken,
                $"line {lineNumber}: token '{token}' is not an integer.");
        }
        catch (OverflowException)
        {
            throw new MapLoadException(
                lineNumber,
                MapLoadException.Rules.NonIntegerToken,
                $"line {lineNumber}: token '{token}' is out of range for a 32-bit integer.");
        }
    }

    private static bool IsValidNameId(string id)
    {
        foreach (var c in id)
        {
            var isLower = c is >= 'a' and <= 'z';
            var isDigit = c is >= '0' and <= '9';
            if (!isLower && !isDigit && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    private static bool TryParseKind(string token, out MapRecordKind kind)
    {
        switch (token)
        {
            case "HKMAP":
                kind = MapRecordKind.Hkmap;
                return true;
            case "NAME":
                kind = MapRecordKind.Name;
                return true;
            case "GRID":
                kind = MapRecordKind.Grid;
                return true;
            case "WALL":
                kind = MapRecordKind.Wall;
                return true;
            case "DOOR":
                kind = MapRecordKind.Door;
                return true;
            case "COVER":
                kind = MapRecordKind.Cover;
                return true;
            case "SPAWN":
                kind = MapRecordKind.Spawn;
                return true;
            case "OBJECTIVE":
                kind = MapRecordKind.Objective;
                return true;
            case "END":
                kind = MapRecordKind.End;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private enum HeaderStage
    {
        ExpectHkmap,
        ExpectName,
        ExpectGrid,
        Body,
    }
}
